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

fun parseExpiryDate(value: String?): Instant? {
	if (value.isNullOrBlank()) return null

	return runCatching { Instant.parse(value) }.getOrElse {
		runCatching { OffsetDateTime.parse(value).toInstant() }.getOrElse {
			runCatching { Instant.parse("${value}Z") }.getOrNull()
		}
	}
}

fun isUserExpired(expiryDate: String?): Boolean {
	val expiryInstant = parseExpiryDate(expiryDate) ?: return false
	return !expiryInstant.isAfter(Instant.now())
}

suspend fun fetchExpiryDate(serverAddress: String, accessToken: String): String? = withContext(Dispatchers.IO) {
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
			JSONObject(response).optString("ExpiryDate", null)
		}.onFailure { error ->
			Timber.w(error, "Unable to parse expiry date response")
		}.getOrNull()
	}?.takeUnless(String::isBlank)
}
