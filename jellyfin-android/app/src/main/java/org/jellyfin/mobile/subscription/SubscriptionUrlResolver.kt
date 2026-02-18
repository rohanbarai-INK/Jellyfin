package org.jellyfin.mobile.subscription

private const val HASH_BANG_SUBSCRIPTION = "#!/subscription"
private const val HASH_SUBSCRIPTION = "#/subscription"
private const val DEFAULT_SUBSCRIPTION_PATH = "/web/index.html#!/subscription"

object SubscriptionUrlResolver {
    fun isSubscriptionUrl(url: String?): Boolean {
        if (url.isNullOrBlank()) return false
        return url.contains(HASH_BANG_SUBSCRIPTION, ignoreCase = true)
            || url.contains(HASH_SUBSCRIPTION, ignoreCase = true)
    }

    fun resolve(serverUrl: String?, redirectUrl: String?): String? {
        val candidate = redirectUrl?.takeUnless(String::isBlank) ?: DEFAULT_SUBSCRIPTION_PATH
        if (candidate.startsWith("http://", ignoreCase = true)
            || candidate.startsWith("https://", ignoreCase = true)
        ) {
            return candidate
        }

        val baseUrl = serverUrl?.takeUnless(String::isBlank)?.trimEnd('/') ?: return null
        return "$baseUrl/${candidate.trimStart('/')}"
    }
}

