package org.jellyfin.androidtv.data.service

import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.MainScope
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import org.jellyfin.androidtv.auth.repository.SessionRepository
import org.jellyfin.androidtv.auth.repository.UserRepository
import org.jellyfin.androidtv.util.sdk.isUsable
import org.jellyfin.sdk.api.client.ApiClient
import org.json.JSONObject
import timber.log.Timber
import java.net.HttpURLConnection
import java.net.URLEncoder
import java.net.URL
import java.nio.charset.StandardCharsets

private const val NORMAL_POLL_DELAY_MS = 10_000L
private const val RECOVERY_POLL_DELAY_MS = 3_000L

enum class MediaMountAutoHealState {
	HEALTHY,
	RECONNECTING,
	RECOVERED,
	DEGRADED,
}

data class MediaMountAutoHealUiState(
	val state: MediaMountAutoHealState,
	val message: String,
	val detail: String? = null,
)

class MediaMountAutoHealStatusService(
	private val api: ApiClient,
	private val sessionRepository: SessionRepository,
	private val userRepository: UserRepository,
) {
	private val scope = MainScope()
	private var pollJob: Job? = null

	private val _status = MutableStateFlow<MediaMountAutoHealUiState?>(null)
	val status: StateFlow<MediaMountAutoHealUiState?> = _status.asStateFlow()

	fun start() {
		if (pollJob?.isActive == true) return

		pollJob = scope.launch {
			while (isActive) {
				val nextDelay = if (canPoll()) {
					runCatching { fetchStatus() }
						.getOrElse { error -> handleFetchFailure(error) }
				} else {
					_status.value = null
					NORMAL_POLL_DELAY_MS
				}

				delay(nextDelay)
			}
		}
	}

	fun stop() {
		pollJob?.cancel()
		pollJob = null
	}

	private fun canPoll(): Boolean {
		return api.isUsable &&
			sessionRepository.currentSession.value != null &&
			userRepository.currentUser.value != null
	}

	private suspend fun fetchStatus(): Long = withContext(Dispatchers.IO) {
		val baseUrl = api.baseUrl?.trimEnd('/') ?: return@withContext NORMAL_POLL_DELAY_MS
		val accessToken = api.accessToken ?: return@withContext NORMAL_POLL_DELAY_MS
		val endpoint = "$baseUrl/System/AutoHeal/Status?api_key=${
			URLEncoder.encode(accessToken, StandardCharsets.UTF_8.name())
		}"

		val request = URL(endpoint).openConnection() as HttpURLConnection
		request.requestMethod = "GET"
		request.connectTimeout = 4000
		request.readTimeout = 4000
		request.setRequestProperty("Accept", "application/json")

		try {
			val body = request.inputStream.bufferedReader().use { it.readText() }
			val status = parseStatus(body)

			_status.value = if (status.state == MediaMountAutoHealState.HEALTHY) null else status
			if (status.state == MediaMountAutoHealState.RECONNECTING) RECOVERY_POLL_DELAY_MS else NORMAL_POLL_DELAY_MS
		} finally {
			request.disconnect()
		}
	}

	private fun parseStatus(json: String): MediaMountAutoHealUiState {
		val payload = JSONObject(json)
		val rawState = payload.optString("State", payload.optString("state", "healthy"))
			.lowercase()
		val rawMessage = payload.optString("Message", payload.optString("message", ""))
		val rawFailureReason = payload.optString("FailureReason", payload.optString("failureReason", ""))
			.takeIf { it.isNotBlank() }

		val state = when (rawState) {
			"reconnecting" -> MediaMountAutoHealState.RECONNECTING
			"recovered" -> MediaMountAutoHealState.RECOVERED
			"degraded" -> MediaMountAutoHealState.DEGRADED
			else -> MediaMountAutoHealState.HEALTHY
		}

		val message = rawMessage.ifBlank {
			when (state) {
				MediaMountAutoHealState.RECONNECTING -> "Media storage is reconnecting. Please wait 30 seconds."
				MediaMountAutoHealState.RECOVERED -> "Playback service has been restored. Please try again."
				MediaMountAutoHealState.DEGRADED -> "Service is temporarily unavailable. Please try again in 1-2 minutes."
				MediaMountAutoHealState.HEALTHY -> ""
			}
		}

		return MediaMountAutoHealUiState(
			state = state,
			message = message,
			detail = rawFailureReason
		)
	}

	private fun handleFetchFailure(error: Throwable): Long {
		Timber.w(error, "Unable to poll media auto-heal status")

		val current = _status.value
		if (current?.state == MediaMountAutoHealState.RECONNECTING) {
			return RECOVERY_POLL_DELAY_MS
		}

		_status.value = MediaMountAutoHealUiState(
			state = MediaMountAutoHealState.RECONNECTING,
			message = "Server is restarting. Please wait 30 seconds.",
			detail = null
		)

		return RECOVERY_POLL_DELAY_MS
	}
}
