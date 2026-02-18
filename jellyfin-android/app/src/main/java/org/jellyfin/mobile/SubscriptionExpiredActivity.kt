package org.jellyfin.mobile

import android.os.Bundle
import android.webkit.WebResourceRequest
import android.webkit.WebView
import android.webkit.WebViewClient
import androidx.activity.addCallback
import androidx.appcompat.app.AppCompatActivity
import org.jellyfin.mobile.databinding.ActivitySubscriptionExpiredBinding
import org.jellyfin.mobile.subscription.SubscriptionUrlResolver
import org.jellyfin.mobile.utils.applyDefault

class SubscriptionExpiredActivity : AppCompatActivity() {
    companion object {
        const val EXTRA_SUBSCRIPTION_URL = "org.jellyfin.mobile.intent.extra.SUBSCRIPTION_URL"
        const val EXTRA_SERVER_URL = "org.jellyfin.mobile.intent.extra.SERVER_URL"
        const val EXTRA_EXPIRY_DATE = "org.jellyfin.mobile.intent.extra.EXPIRY_DATE"
    }

    private lateinit var binding: ActivitySubscriptionExpiredBinding
    private lateinit var subscriptionUrl: String

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        binding = ActivitySubscriptionExpiredBinding.inflate(layoutInflater)
        setContentView(binding.root)

        val explicitSubscriptionUrl = intent.getStringExtra(EXTRA_SUBSCRIPTION_URL)
        val serverUrl = intent.getStringExtra(EXTRA_SERVER_URL)
        subscriptionUrl = SubscriptionUrlResolver.resolve(serverUrl, explicitSubscriptionUrl) ?: run {
            finishAfterTransition()
            return
        }

        binding.subscriptionWebView.apply {
            webViewClient = SubscriptionWebViewClient()
            settings.applyDefault()
            loadUrl(subscriptionUrl)
        }

        onBackPressedDispatcher.addCallback(this, enabled = true) {
            moveTaskToBack(true)
        }
    }

    private inner class SubscriptionWebViewClient : WebViewClient() {
        override fun shouldOverrideUrlLoading(view: WebView, request: WebResourceRequest): Boolean {
            if (SubscriptionUrlResolver.isSubscriptionUrl(request.url.toString())) {
                return false
            }

            view.loadUrl(subscriptionUrl)
            return true
        }
    }
}
