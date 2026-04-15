package org.knightflix.mobile

import android.os.Bundle
import android.webkit.WebResourceRequest
import android.webkit.WebView
import android.webkit.WebViewClient
import androidx.activity.addCallback
import androidx.appcompat.app.AppCompatActivity
import androidx.core.view.ViewCompat
import org.knightflix.mobile.databinding.ActivitySubscriptionExpiredBinding
import org.knightflix.mobile.subscription.SubscriptionUrlResolver
import org.knightflix.mobile.utils.applyDefault
import org.knightflix.mobile.utils.applyWindowInsetsAsMargins

class SubscriptionExpiredActivity : AppCompatActivity() {
    companion object {
        const val EXTRA_SUBSCRIPTION_URL = "org.knightflix.mobile.intent.extra.SUBSCRIPTION_URL"
        const val EXTRA_SERVER_URL = "org.knightflix.mobile.intent.extra.SERVER_URL"
        const val EXTRA_EXPIRY_DATE = "org.knightflix.mobile.intent.extra.EXPIRY_DATE"
    }

    private lateinit var binding: ActivitySubscriptionExpiredBinding
    private lateinit var subscriptionUrl: String

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        binding = ActivitySubscriptionExpiredBinding.inflate(layoutInflater)
        setContentView(binding.root)
        binding.root.applyWindowInsetsAsMargins()
        ViewCompat.requestApplyInsets(binding.root)

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
