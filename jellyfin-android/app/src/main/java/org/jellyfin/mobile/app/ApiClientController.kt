package org.jellyfin.mobile.app

import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import org.jellyfin.mobile.data.dao.ServerDao
import org.jellyfin.mobile.data.dao.UserDao
import org.jellyfin.mobile.data.entity.ServerEntity
import org.jellyfin.sdk.Jellyfin
import org.jellyfin.sdk.api.client.ApiClient
import org.jellyfin.sdk.model.DeviceInfo
import org.json.JSONException
import org.json.JSONObject
import timber.log.Timber
import java.net.HttpURLConnection
import java.net.URLEncoder
import java.net.URL
import java.nio.charset.StandardCharsets
import java.time.Instant
import java.time.OffsetDateTime
import java.time.format.DateTimeParseException

class ApiClientController(
    private val appPreferences: AppPreferences,
    private val jellyfin: Jellyfin,
    private val apiClient: ApiClient,
    private val serverDao: ServerDao,
    private val userDao: UserDao,
) {
    data class SavedServerUser(
        val server: ServerEntity,
        val userId: String,
        val accessToken: String,
    )

    data class UserExpiryStatus(
        val expiryDateRaw: String?,
        val expiryDate: Instant?,
        val isInGracePeriod: Boolean,
        val graceDaysRemaining: Int,
        val isExpired: Boolean,
    )

    private val baseDeviceInfo: DeviceInfo
        get() = jellyfin.options.deviceInfo!!

    /**
     * Store server with [hostname] in the database.
     */
    suspend fun setupServer(hostname: String) {
        appPreferences.currentServerId = withContext(Dispatchers.IO) {
            serverDao.getServerByHostname(hostname)?.id ?: serverDao.insert(hostname)
        }
        apiClient.update(baseUrl = hostname)
    }

    suspend fun setupUser(serverId: Long, userId: String, accessToken: String) {
        appPreferences.currentUserId = withContext(Dispatchers.IO) {
            userDao.upsert(serverId, userId, accessToken)
        }
        configureApiClientUser(userId, accessToken)
    }

    suspend fun loadSavedServer(): ServerEntity? {
        val server = withContext(Dispatchers.IO) {
            val serverId = appPreferences.currentServerId ?: return@withContext null
            serverDao.getServer(serverId)
        }
        configureApiClientServer(server)
        return server
    }

    suspend fun loadSavedServerUser(): SavedServerUser? {
        val serverUser = withContext(Dispatchers.IO) {
            val serverId = appPreferences.currentServerId ?: return@withContext null
            val userId = appPreferences.currentUserId ?: return@withContext null
            userDao.getServerUser(serverId, userId)
        }

        configureApiClientServer(serverUser?.server)

        if (serverUser?.user?.accessToken != null) {
            configureApiClientUser(serverUser.user.userId, serverUser.user.accessToken)
            return SavedServerUser(
                server = serverUser.server,
                userId = serverUser.user.userId,
                accessToken = serverUser.user.accessToken,
            )
        } else {
            resetApiClientUser()
        }

        return null
    }

    suspend fun loadPreviouslyUsedServers(): List<ServerEntity> = withContext(Dispatchers.IO) {
        serverDao.getAllServers().filterNot { server ->
            server.id == appPreferences.currentServerId
        }
    }

    private fun configureApiClientServer(server: ServerEntity?) {
        apiClient.update(baseUrl = server?.hostname)
    }

    private fun configureApiClientUser(userId: String, accessToken: String) {
        apiClient.update(
            accessToken = accessToken,
            // Append user id to device id to ensure uniqueness across sessions
            deviceInfo = baseDeviceInfo.copy(id = baseDeviceInfo.id + userId),
        )
    }

    private fun resetApiClientUser() {
        apiClient.update(
            accessToken = null,
            deviceInfo = baseDeviceInfo,
        )
    }

    suspend fun getUserExpiryStatus(server: ServerEntity, accessToken: String): UserExpiryStatus = withContext(Dispatchers.IO) {
        val baseUrl = server.hostname.trimEnd('/')
        val endpoint = "$baseUrl/Users/Me?api_key=${
            URLEncoder.encode(accessToken, StandardCharsets.UTF_8.name())
        }"
        val request = URL(endpoint).openConnection() as HttpURLConnection
        request.requestMethod = "GET"
        request.connectTimeout = 10000
        request.readTimeout = 10000
        request.setRequestProperty("Accept", "application/json")

        val body = try {
            request.inputStream.bufferedReader().use { it.readText() }
        } catch (error: Exception) {
            val message = request.errorStream?.bufferedReader()?.use { it.readText() }
            Timber.w(error, "Unable to retrieve current user for expiry check: %s", message)
            null
        } finally {
            request.disconnect()
        }

        val expiryDateRaw = body
            ?.let { response ->
                try {
                    JSONObject(response).optString("ExpiryDate", null)
                } catch (error: JSONException) {
                    Timber.w(error, "Unable to parse user response")
                    null
                }
            }
            ?.takeUnless(String::isBlank)
        val userPayload = body
            ?.let { response ->
                runCatching { JSONObject(response) }
                    .onFailure { error -> Timber.w(error, "Unable to parse user response") }
                    .getOrNull()
            }
        val isInGracePeriod = userPayload?.optBoolean("IsInGracePeriod", false) == true
        val graceDaysRemaining = userPayload?.optInt("GraceDaysRemaining", 0)?.coerceAtLeast(0) ?: 0
        val expiryDate = parseExpiryDate(expiryDateRaw)
        val isExpiredByDate = expiryDate != null && !expiryDate.isAfter(Instant.now())
        UserExpiryStatus(
            expiryDateRaw = expiryDateRaw,
            expiryDate = expiryDate,
            isInGracePeriod = isInGracePeriod,
            graceDaysRemaining = graceDaysRemaining,
            isExpired = isExpiredByDate && !isInGracePeriod,
        )
    }

    private fun parseExpiryDate(value: String?): Instant? {
        if (value.isNullOrBlank()) return null

        return runCatching { Instant.parse(value) }.getOrElse {
            runCatching { OffsetDateTime.parse(value).toInstant() }.getOrElse {
                runCatching { Instant.parse("${value}Z") }.getOrElse { error ->
                    if (error is DateTimeParseException) {
                        Timber.w(error, "Unable to parse ExpiryDate: %s", value)
                    }
                    null
                }
            }
        }
    }
}
