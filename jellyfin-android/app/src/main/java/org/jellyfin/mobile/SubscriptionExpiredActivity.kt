package org.jellyfin.mobile

import android.content.Intent
import android.os.Bundle
import androidx.appcompat.app.AppCompatActivity
import androidx.core.net.toUri
import org.jellyfin.mobile.databinding.ActivitySubscriptionExpiredBinding

class SubscriptionExpiredActivity : AppCompatActivity() {
    companion object {
        const val EXTRA_REDEMPTION_URL = "org.jellyfin.mobile.intent.extra.REDEMPTION_URL"
        const val EXTRA_EXPIRY_DATE = "org.jellyfin.mobile.intent.extra.EXPIRY_DATE"
    }

    private lateinit var binding: ActivitySubscriptionExpiredBinding

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        binding = ActivitySubscriptionExpiredBinding.inflate(layoutInflater)
        setContentView(binding.root)

        val redemptionUrl = intent.getStringExtra(EXTRA_REDEMPTION_URL)
        val expiryDate = intent.getStringExtra(EXTRA_EXPIRY_DATE)

        if (!expiryDate.isNullOrBlank()) {
            binding.description.text = getString(R.string.subscription_expired_message_with_date, expiryDate)
        }

        binding.manageSubscriptionButton.setOnClickListener {
            redemptionUrl?.let { url ->
                startActivity(Intent(Intent.ACTION_VIEW, url.toUri()))
            }
        }
        binding.closeButton.setOnClickListener {
            finishAfterTransition()
        }
    }
}
