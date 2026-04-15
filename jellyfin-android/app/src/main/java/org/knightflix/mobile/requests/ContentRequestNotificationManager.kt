package org.knightflix.mobile.requests

import android.Manifest
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.content.Context
import android.content.Intent
import android.content.pm.PackageManager
import android.graphics.Bitmap
import android.graphics.BitmapFactory
import androidx.core.app.NotificationCompat
import androidx.core.app.NotificationManagerCompat
import androidx.core.content.ContextCompat
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import kotlinx.coroutines.withContext
import org.knightflix.mobile.MainActivity
import org.knightflix.mobile.R
import org.knightflix.mobile.app.ApiClientController
import org.knightflix.mobile.ui.content.ImageProvider
import org.knightflix.mobile.utils.AndroidVersion
import org.knightflix.mobile.utils.Constants
import org.jellyfin.sdk.model.api.ImageType
import org.jellyfin.sdk.model.serializer.toUUIDOrNull
import org.json.JSONArray
import org.json.JSONObject
import timber.log.Timber
import java.net.HttpURLConnection
import java.net.URL
import java.net.URLEncoder
import java.nio.charset.StandardCharsets

private const val NETWORK_TIMEOUT_MS = 10000

class ContentRequestNotificationManager(
    private val context: Context,
    private val apiClientController: ApiClientController,
) {
    private val appContext = context.applicationContext
    private val syncMutex = Mutex()

    suspend fun syncAndNotify() {
        syncMutex.withLock {
            val serverUser = apiClientController.loadSavedServerUser() ?: return
            if (!canPostNotifications()) return

            createNotificationChannelIfNeeded()
            val rows = fetchRequestNotifications(serverUser.server.hostname, serverUser.accessToken)
            if (rows.isEmpty()) return

            val deliveredRequestIds = mutableListOf<String>()
            rows.forEach { row ->
                if (postNotification(row)) {
                    deliveredRequestIds.add(row.requestId)
                }
            }

            if (deliveredRequestIds.isNotEmpty()) {
                markNotificationViewed(serverUser.server.hostname, serverUser.accessToken, deliveredRequestIds)
            }
        }
    }

    private suspend fun postNotification(row: RequestNotificationRow): Boolean = withContext(Dispatchers.IO) {
        val itemId = row.jellyfinItemId ?: return@withContext false
        val manager = NotificationManagerCompat.from(appContext)
        if (!manager.areNotificationsEnabled()) return@withContext false

        val notificationId = (Constants.CONTENT_REQUEST_NOTIFICATION_PREFIX + row.requestId).hashCode()
        val launchIntent = Intent(appContext, MainActivity::class.java).apply {
            flags = Intent.FLAG_ACTIVITY_CLEAR_TOP or Intent.FLAG_ACTIVITY_SINGLE_TOP
            putExtra(Constants.EXTRA_REQUEST_CONTENT_ITEM_ID, itemId)
        }
        val contentIntent = PendingIntent.getActivity(
            appContext,
            notificationId,
            launchIntent,
            Constants.PENDING_INTENT_FLAGS,
        )

        val (titleRes, bodyRes) = when (row.type) {
            RequestMediaType.Movie -> R.string.request_notification_movie_title to R.string.request_notification_movie_body
            RequestMediaType.Series -> if (row.seasonNumber != null && row.seasonNumber > 0) {
                R.string.request_notification_series_title to R.string.request_notification_series_body_with_season
            } else {
                R.string.request_notification_series_title to R.string.request_notification_series_body
            }
        }

        val contentBody = when (row.type) {
            RequestMediaType.Movie -> appContext.getString(bodyRes, row.title)
            RequestMediaType.Series -> {
                if (row.seasonNumber != null && row.seasonNumber > 0) {
                    appContext.getString(bodyRes, row.title, row.seasonNumber)
                } else {
                    appContext.getString(bodyRes, row.title)
                }
            }
        }

        val imageBitmap = loadNotificationBitmap(itemId, row.type)
        val builder = NotificationCompat.Builder(appContext, Constants.CONTENT_REQUEST_NOTIFICATION_CHANNEL_ID)
            .setSmallIcon(
                when (row.type) {
                    RequestMediaType.Movie -> R.drawable.ic_notification_request_movie
                    RequestMediaType.Series -> R.drawable.ic_notification_request_series
                },
            )
            .setContentTitle(appContext.getString(titleRes))
            .setContentText(contentBody)
            .setPriority(NotificationCompat.PRIORITY_HIGH)
            .setCategory(NotificationCompat.CATEGORY_RECOMMENDATION)
            .setContentIntent(contentIntent)
            .setAutoCancel(true)
            .setOnlyAlertOnce(true)

        if (imageBitmap != null) {
            builder
                .setLargeIcon(imageBitmap)
                .setStyle(
                    NotificationCompat.BigPictureStyle()
                        .bigPicture(imageBitmap)
                        .bigLargeIcon(null as Bitmap?),
                )
        } else {
            builder.setStyle(NotificationCompat.BigTextStyle().bigText(contentBody))
        }

        manager.notify(notificationId, builder.build())
        true
    }

    private fun createNotificationChannelIfNeeded() {
        if (!AndroidVersion.isAtLeastO) return

        val notificationManager = appContext.getSystemService(Context.NOTIFICATION_SERVICE) as? NotificationManager ?: return
        val channel = NotificationChannel(
            Constants.CONTENT_REQUEST_NOTIFICATION_CHANNEL_ID,
            appContext.getString(R.string.request_notification_channel_name),
            NotificationManager.IMPORTANCE_HIGH,
        ).apply {
            description = appContext.getString(R.string.request_notification_channel_description)
            setShowBadge(true)
        }
        notificationManager.createNotificationChannel(channel)
    }

    private suspend fun loadNotificationBitmap(itemId: String, mediaType: RequestMediaType): Bitmap? = withContext(Dispatchers.IO) {
        val uuid = itemId.toUUIDOrNull() ?: return@withContext null
        val preferredType = when (mediaType) {
            RequestMediaType.Movie -> ImageType.PRIMARY
            RequestMediaType.Series -> ImageType.BACKDROP
        }

        decodeBitmap(preferredType, uuid)
            ?: if (preferredType != ImageType.PRIMARY) decodeBitmap(ImageType.PRIMARY, uuid) else null
    }

    private fun decodeBitmap(imageType: ImageType, itemId: java.util.UUID): Bitmap? {
        val uri = ImageProvider.buildItemUri(itemId, imageType, null)
        return runCatching {
            appContext.contentResolver.openInputStream(uri)?.use(BitmapFactory::decodeStream)
        }.onFailure {
            Timber.d(it, "Unable to load request notification image for %s", itemId)
        }.getOrNull()
    }

    private fun fetchRequestNotifications(baseUrl: String, accessToken: String): List<RequestNotificationRow> {
        val payload = fetchEndpointJson(baseUrl, "Request/Notifications", accessToken) ?: return emptyList()
        return parseRequestRows(payload)
    }

    private fun markNotificationViewed(baseUrl: String, accessToken: String, requestIds: List<String>) {
        val body = JSONObject().put("RequestIds", JSONArray(requestIds)).toString()
        val result = requestEndpoint(
            baseUrl = baseUrl,
            path = "Request/NotificationViewedBulk",
            accessToken = accessToken,
            method = "POST",
            body = body,
        )
        if (result == null || !result.isSuccessful) {
            Timber.w(
                "Failed to mark request notifications viewed. Code=%s body=%s",
                result?.statusCode?.toString() ?: "null",
                result?.responseBody,
            )
        }
    }

    private fun fetchEndpointJson(baseUrl: String, path: String, accessToken: String): String? {
        val result = requestEndpoint(baseUrl, path, accessToken, method = "GET")
        if (result == null || !result.isSuccessful) {
            Timber.w(
                "Request failed for %s with code %s. Body: %s",
                path,
                result?.statusCode?.toString() ?: "null",
                result?.responseBody,
            )
            return null
        }

        return result.responseBody
    }

    private fun requestEndpoint(
        baseUrl: String,
        path: String,
        accessToken: String,
        method: String,
        body: String? = null,
    ): EndpointResult? {
        val normalizedBaseUrl = baseUrl.trimEnd('/')
        val normalizedPath = path.trimStart('/')
        val endpoint = "$normalizedBaseUrl/$normalizedPath?api_key=${
            URLEncoder.encode(accessToken, StandardCharsets.UTF_8.name())
        }"
        val request = URL(endpoint).openConnection() as HttpURLConnection
        request.requestMethod = method
        request.connectTimeout = NETWORK_TIMEOUT_MS
        request.readTimeout = NETWORK_TIMEOUT_MS
        request.setRequestProperty("Accept", "application/json")
        if (!body.isNullOrBlank()) {
            request.setRequestProperty("Content-Type", "application/json")
            request.doOutput = true
            request.outputStream.bufferedWriter(StandardCharsets.UTF_8).use { writer ->
                writer.write(body)
            }
        }

        return try {
            val statusCode = request.responseCode
            val stream = if (statusCode in 200..299) request.inputStream else request.errorStream
            val responseBody = stream?.bufferedReader()?.use { it.readText() }
            EndpointResult(statusCode = statusCode, responseBody = responseBody)
        } catch (error: Exception) {
            Timber.w(error, "Failed to request %s", path)
            null
        } finally {
            request.disconnect()
        }
    }

    private fun parseRequestRows(payload: String): List<RequestNotificationRow> {
        val rowsJson = runCatching { JSONArray(payload) }.getOrElse { error ->
            Timber.w(error, "Unable to parse request notifications payload")
            return emptyList()
        }

        return buildList {
            for (index in 0 until rowsJson.length()) {
                val row = rowsJson.optJSONObject(index) ?: continue
                val requestId = row.optString("Id").normalizeServerValue() ?: continue
                val title = row.optString("Title").normalizeServerValue() ?: continue
                val jellyfinItemId = row.optString("JellyfinItemId").normalizeServerValue() ?: continue
                val type = RequestMediaType.fromServerValue(row.opt("Type"))
                val seasonNumber = row.opt("SeasonNumber").toNonZeroIntOrNull()

                add(
                    RequestNotificationRow(
                        requestId = requestId,
                        title = title,
                        type = type,
                        seasonNumber = seasonNumber,
                        jellyfinItemId = jellyfinItemId,
                    ),
                )
            }
        }
    }

    private fun canPostNotifications(): Boolean {
        if (!AndroidVersion.isAtLeastT) return true
        return ContextCompat.checkSelfPermission(appContext, Manifest.permission.POST_NOTIFICATIONS) == PackageManager.PERMISSION_GRANTED
    }

    private fun Any?.toNonZeroIntOrNull(): Int? {
        val value = when (this) {
            is Number -> toInt()
            is String -> toIntOrNull()
            else -> null
        }
        return value?.takeIf { it > 0 }
    }

    private fun String?.normalizeServerValue(): String? {
        if (this == null) return null
        val normalized = trim()
        if (normalized.isEmpty()) return null
        if (normalized.equals("null", ignoreCase = true)) return null
        return normalized
    }

    private data class EndpointResult(
        val statusCode: Int,
        val responseBody: String?,
    ) {
        val isSuccessful: Boolean
            get() = statusCode in 200..299
    }

    private data class RequestNotificationRow(
        val requestId: String,
        val title: String,
        val type: RequestMediaType,
        val seasonNumber: Int?,
        val jellyfinItemId: String?,
    )

    private enum class RequestMediaType {
        Movie,
        Series;

        companion object {
            fun fromServerValue(value: Any?): RequestMediaType {
                return when (value) {
                    is Number -> if (value.toInt() == 1) Series else Movie
                    is String -> if (value.equals("Series", ignoreCase = true) || value == "1") Series else Movie
                    else -> Movie
                }
            }
        }
    }
}
