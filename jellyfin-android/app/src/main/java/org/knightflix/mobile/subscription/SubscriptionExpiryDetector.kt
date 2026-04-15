package org.knightflix.mobile.subscription

import org.json.JSONException
import org.json.JSONObject
import timber.log.Timber
import java.util.Locale

data class SubscriptionExpiryInfo(
    val redirectUrl: String? = null,
    val expiryDate: String? = null,
)

object SubscriptionExpiryDetector {
    private const val SUBSCRIPTION_EXPIRED_CODE = "SubscriptionExpired"
    private val expiryHeaderNames = setOf("x-subscription-expired", "subscription-expired")
    private val expiryCodeHeaderNames = setOf("x-error-code", "x-jellyfin-error-code")

    fun detect(
        statusCode: Int,
        headers: Map<String, List<String>> = emptyMap(),
        responseBody: String? = null,
    ): SubscriptionExpiryInfo? {
        if (statusCode != 403) return null

        val normalizedHeaders = headers
            .filterKeys { key -> !key.isNullOrBlank() }
            .mapKeys { (key, _) -> key.lowercase(Locale.ROOT) }

        val hasExpiryHeader = expiryHeaderNames.any { name ->
            normalizedHeaders[name]?.any { value -> value.equals("true", ignoreCase = true) } == true
        }

        val hasExpiryCodeHeader = expiryCodeHeaderNames.any { name ->
            normalizedHeaders[name]?.any { value ->
                value.equals(SUBSCRIPTION_EXPIRED_CODE, ignoreCase = true)
            } == true
        }

        val bodyInfo = parseResponseBody(responseBody)
        return when {
            hasExpiryHeader || hasExpiryCodeHeader -> bodyInfo ?: SubscriptionExpiryInfo()
            bodyInfo != null -> bodyInfo
            else -> null
        }
    }

    private fun parseResponseBody(responseBody: String?): SubscriptionExpiryInfo? {
        if (responseBody.isNullOrBlank()) return null

        val fallbackMatch = responseBody.contains(SUBSCRIPTION_EXPIRED_CODE, ignoreCase = true)
            || (
                responseBody.contains("subscription", ignoreCase = true)
                    && responseBody.contains("expired", ignoreCase = true)
                )

        return try {
            val payload = JSONObject(responseBody)
            val code = payload.optString("code", "")
            val message = payload.optString("message", "")
            val redirectUrl = payload.optString("redirectUrl").takeUnless(String::isBlank)
            val expiryDate = payload.optString("expiryDate").takeUnless(String::isBlank)
                ?: payload.optString("ExpiryDate").takeUnless(String::isBlank)

            val isExpiredPayload = code.equals(SUBSCRIPTION_EXPIRED_CODE, ignoreCase = true)
                || message.contains("subscription", ignoreCase = true)
                || (!redirectUrl.isNullOrBlank() && SubscriptionUrlResolver.isSubscriptionUrl(redirectUrl))

            if (isExpiredPayload) SubscriptionExpiryInfo(redirectUrl = redirectUrl, expiryDate = expiryDate) else null
        } catch (error: JSONException) {
            if (!fallbackMatch) {
                null
            } else {
                Timber.v(error, "Unable to parse subscription expiry payload")
                SubscriptionExpiryInfo()
            }
        }
    }
}
