package org.jellyfin.androidtv.ui.settings.screen

import android.content.Intent
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.res.stringResource
import kotlinx.coroutines.launch
import org.jellyfin.androidtv.R
import org.jellyfin.androidtv.auth.model.fetchUserAccessState
import org.jellyfin.androidtv.auth.model.isUserExpired
import org.jellyfin.androidtv.auth.repository.SessionRepository
import org.jellyfin.androidtv.auth.repository.UserRepository
import org.jellyfin.androidtv.auth.store.AuthenticationStore
import org.jellyfin.androidtv.ui.base.Icon
import org.jellyfin.androidtv.ui.base.Text
import org.jellyfin.androidtv.ui.base.list.ListButton
import org.jellyfin.androidtv.ui.base.list.ListSection
import org.jellyfin.androidtv.ui.navigation.LocalRouter
import org.jellyfin.androidtv.ui.settings.Routes
import org.jellyfin.androidtv.ui.settings.composable.SettingsColumn
import org.jellyfin.androidtv.ui.startup.SubscriptionExpiredActivity
import org.jellyfin.androidtv.ui.subscription.SubscriptionManagementActivity
import org.jellyfin.androidtv.util.sdk.expiryDateRaw
import org.koin.compose.koinInject

@Composable
fun SettingsMainScreen() {
	val context = LocalContext.current
	val router = LocalRouter.current
	val scope = rememberCoroutineScope()
	val sessionRepository = koinInject<SessionRepository>()
	val userRepository = koinInject<UserRepository>()
	val authenticationStore = koinInject<AuthenticationStore>()
	val session by sessionRepository.currentSession.collectAsState()
	val currentUser by userRepository.currentUser.collectAsState()

	SettingsColumn {
		item {
			ListSection(
				overlineContent = { Text(stringResource(R.string.app_name).uppercase()) },
				headingContent = { Text(stringResource(R.string.settings)) },
				captionContent = { Text(stringResource(R.string.settings_description)) },
			)
		}

		item {
			ListButton(
				leadingContent = { Icon(painterResource(R.drawable.ic_users), contentDescription = null) },
				headingContent = { Text(stringResource(R.string.pref_login)) },
				onClick = { router.push(Routes.AUTHENTICATION) },
			)
		}

		if (session != null) {
			item {
				ListButton(
					leadingContent = { Icon(painterResource(R.drawable.ic_star), contentDescription = null) },
					headingContent = { Text(stringResource(R.string.pref_subscription)) },
					captionContent = { Text(stringResource(R.string.pref_subscription_description)) },
					onClick = {
						val activeSession = session ?: return@ListButton
						scope.launch {
							val serverAddress = authenticationStore.getServer(activeSession.serverId)?.address
							val userAccessState = if (!serverAddress.isNullOrBlank()) {
								fetchUserAccessState(serverAddress, activeSession.accessToken)
							} else {
								null
							}
							val fallbackExpiryDate = currentUser?.expiryDateRaw()
							val expiryDate = userAccessState?.expiryDateRaw ?: fallbackExpiryDate
							val shouldBlock = if (userAccessState != null) {
								isUserExpired(userAccessState)
							} else {
								isUserExpired(fallbackExpiryDate)
							}

							if (shouldBlock) {
								context.startActivity(
									Intent(context, SubscriptionExpiredActivity::class.java).apply {
										putExtra(SubscriptionExpiredActivity.EXTRA_EXPIRY_DATE, expiryDate)
									}
								)
							} else {
								context.startActivity(Intent(context, SubscriptionManagementActivity::class.java))
							}
						}
					}
				)
			}
		}

		item {
			ListButton(
				leadingContent = { Icon(painterResource(R.drawable.ic_adjust), contentDescription = null) },
				headingContent = { Text(stringResource(R.string.pref_customization)) },
				onClick = { router.push(Routes.CUSTOMIZATION) }
			)
		}

		// TODO: Temporarily added to root - should be accessed via customization screen instead
		item {
			ListButton(
				leadingContent = { Icon(painterResource(R.drawable.ic_photos), contentDescription = null) },
				headingContent = { Text(stringResource(R.string.pref_screensaver)) },
				onClick = { router.push(Routes.CUSTOMIZATION_SCREENSAVER) }
			)
		}

		item {
			ListButton(
				leadingContent = { Icon(painterResource(R.drawable.ic_next), contentDescription = null) },
				headingContent = { Text(stringResource(R.string.pref_playback)) },
				onClick = { router.push(Routes.PLAYBACK) }
			)
		}

		item {
			ListButton(
				leadingContent = { Icon(painterResource(R.drawable.ic_error), contentDescription = null) },
				headingContent = { Text(stringResource(R.string.pref_telemetry_category)) },
				onClick = { router.push(Routes.TELEMETRY) }
			)

		}

		item {
			ListButton(
				leadingContent = { Icon(painterResource(R.drawable.ic_flask), contentDescription = null) },
				headingContent = { Text(stringResource(R.string.pref_developer_link)) },
				onClick = { router.push(Routes.DEVELOPER) }
			)
		}

		item {
			ListButton(
				leadingContent = { Icon(painterResource(R.drawable.ic_jellyfin), contentDescription = null) },
				headingContent = { Text(stringResource(R.string.pref_about_title)) },
				onClick = { router.push(Routes.ABOUT) }
			)
		}
	}
}
