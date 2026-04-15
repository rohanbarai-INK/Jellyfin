package org.knightflix.mobile.subscription

import okhttp3.Interceptor
import okhttp3.Response
import org.knightflix.mobile.events.ActivityEvent
import org.knightflix.mobile.events.ActivityEventHandler

private const val MAX_ERROR_BODY_BYTES = 64L * 1024L

class SubscriptionExpiryInterceptor(
    private val activityEventHandler: ActivityEventHandler,
) : Interceptor {
    override fun intercept(chain: Interceptor.Chain): Response {
        val request = chain.request()
        val response = chain.proceed(chain.request())
        val responseBody = runCatching { response.peekBody(MAX_ERROR_BODY_BYTES).string() }.getOrNull()
        val expiryInfo = SubscriptionExpiryDetector.detect(
            statusCode = response.code,
            headers = response.headers.toMultimap(),
            responseBody = responseBody,
        )

        if (expiryInfo != null) {
            val baseUrl = request.url.newBuilder()
                .query(null)
                .encodedPath("")
                .build()
                .toString()
                .trimEnd('/')

            activityEventHandler.emit(
                ActivityEvent.SubscriptionExpired(
                    redirectUrl = SubscriptionUrlResolver.resolve(baseUrl, expiryInfo.redirectUrl),
                    expiryDate = expiryInfo.expiryDate,
                ),
            )
        }

        return response
    }
}
