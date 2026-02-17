package org.jellyfin.androidtv.ui.startup

import android.content.Intent
import android.os.Bundle
import androidx.appcompat.app.AppCompatActivity
import org.jellyfin.androidtv.auth.repository.SessionRepository
import org.jellyfin.androidtv.auth.store.AuthenticationPreferences
import org.jellyfin.androidtv.auth.store.AuthenticationStore
import org.jellyfin.androidtv.databinding.ActivitySubscriptionExpiredBinding
import org.jellyfin.androidtv.R
import org.koin.android.ext.android.inject

class SubscriptionExpiredActivity : AppCompatActivity() {
	companion object {
		const val EXTRA_EXPIRY_DATE = "org.jellyfin.androidtv.intent.extra.EXPIRY_DATE"
	}

	private val sessionRepository: SessionRepository by inject()
	private val authenticationStore: AuthenticationStore by inject()
	private val authenticationPreferences: AuthenticationPreferences by inject()

	private lateinit var binding: ActivitySubscriptionExpiredBinding

	override fun onCreate(savedInstanceState: Bundle?) {
		super.onCreate(savedInstanceState)
		binding = ActivitySubscriptionExpiredBinding.inflate(layoutInflater)
		setContentView(binding.root)

		val expiryDate = intent.getStringExtra(EXTRA_EXPIRY_DATE)
		if (!expiryDate.isNullOrBlank()) {
			binding.message.text = getString(R.string.subscription_expired_message_with_date, expiryDate)
		}
		binding.renewalPrompt.text = getString(R.string.subscription_expired_qr_placeholder, "jellyfin.org/redeem")

		binding.logOutButton.setOnClickListener {
			logoutAndReturnToServerSelection()
		}
	}

	private fun logoutAndReturnToServerSelection() {
		sessionRepository.currentSession.value?.let { session ->
			authenticationStore.getUser(session.serverId, session.userId)?.let { user ->
				authenticationStore.putUser(session.serverId, session.userId, user.copy(accessToken = null))
			}
		}

		authenticationPreferences[AuthenticationPreferences.lastServerId] = ""
		authenticationPreferences[AuthenticationPreferences.lastUserId] = ""
		sessionRepository.destroyCurrentSession()

		startActivity(Intent(this, StartupActivity::class.java).apply {
			putExtra(StartupActivity.EXTRA_HIDE_SPLASH, true)
			putExtra(StartupActivity.EXTRA_FORCE_SERVER_SELECTION, true)
			addFlags(Intent.FLAG_ACTIVITY_NEW_TASK or Intent.FLAG_ACTIVITY_CLEAR_TASK)
		})
		finishAfterTransition()
	}
}
