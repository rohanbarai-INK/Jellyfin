package org.jellyfin.androidtv.auth.model

import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import org.json.JSONObject
import timber.log.Timber
import java.net.HttpURLConnection
import java.net.URLEncoder
import java.net.URL
import java.nio.charset.StandardCharsets
import java.time.Instant
import java.time.OffsetDateTime

data class UserAccessState(
	val expiryDateRaw: String?,
	val isInGracePeriod: Boolean,
	val graceDaysRemaining: Int,
) {
	val isExpired: Boolean
		get() {
			val expiryInstant = parseExpiryDate(expiryDateRaw) ?: return false
			return !expiryInstant.isAfter(Instant.now()) && !isInGracePeriod
		}
}

fun parseExpiryDate(value: String?): Instant? {
	if (value.isNullOrBlank()) return null

	return runCatching { Instant.parse(value) }.getOrElse {
		runCatching { OffsetDateTime.parse(value).toInstant() }.getOrElse {
			runCatching { Instant.parse("${value}Z") }.getOrNull()
		}
	}
}

fun isUserExpired(accessState: UserAccessState?): Boolean {
	return accessState?.isExpired == true
}

fun isUserExpired(expiryDate: String?, isInGracePeriod: Boolean = false): Boolean {
	val expiryInstant = parseExpiryDate(expiryDate) ?: return false
	return !expiryInstant.isAfter(Instant.now()) && !isInGracePeriod
}

suspend fun fetchExpiryDate(serverAddress: String, accessToken: String): String? = withContext(Dispatchers.IO) {
	fetchUserAccessState(serverAddress, accessToken)?.expiryDateRaw
}

suspend fun fetchUserAccessState(serverAddress: String, accessToken: String): UserAccessState? = withContext(Dispatchers.IO) {
	val baseUrl = serverAddress.trimEnd('/')
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
		Timber.w(error, "Unable to fetch expiry date: %s", message)
		null
	} finally {
		request.disconnect()
	}

	body?.let { response ->
		runCatching {
			val payload = JSONObject(response)
			val expiryDate = payload.optString("ExpiryDate", null).takeUnless(String::isBlank)
			val isInGracePeriod = payload.optBoolean("IsInGracePeriod", false)
			val graceDaysRemaining = payload.optInt("GraceDaysRemaining", 0).coerceAtLeast(0)
			UserAccessState(
				expiryDateRaw = expiryDate,
				isInGracePeriod = isInGracePeriod,
				graceDaysRemaining = graceDaysRemaining,
			)
		}.onFailure { error ->
			Timber.w(error, "Unable to parse user access response")
		}.getOrNull()
	}
}
