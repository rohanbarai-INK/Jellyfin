package org.knightflix.mobile

import android.app.Application
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.launch
import org.knightflix.mobile.app.ApiClientController
import org.knightflix.mobile.data.entity.ServerEntity

class MainViewModel(
    app: Application,
    private val apiClientController: ApiClientController,
) : AndroidViewModel(app) {
    private val hardcodedServerUrl: String
        get() = normalizeServerUrl(BuildConfig.HARDCODED_SERVER_URL)

    private val hardcodedFallbackServerUrl: String
        get() = normalizeServerUrl(BuildConfig.HARDCODED_FALLBACK_SERVER_URL)

    private val hardcodedServerEnabled: Boolean
        get() = hardcodedServerUrl.isNotEmpty() || hardcodedFallbackServerUrl.isNotEmpty()

    private var selectedHardcodedServerUrl: String? = null

    private val _serverState: MutableStateFlow<ServerState> = MutableStateFlow(ServerState.Pending)
    val serverState: StateFlow<ServerState> get() = _serverState

    init {
        viewModelScope.launch {
            refreshServer()
        }
    }

    suspend fun switchServer(hostname: String) {
        apiClientController.setupServer(hostname)
        refreshServer()
    }

    fun trySwitchToFallbackServer(currentHostname: String): Boolean {
        val primaryUrl = hardcodedServerUrl
        val fallbackUrl = hardcodedFallbackServerUrl
        if (fallbackUrl.isEmpty() || fallbackUrl.equals(primaryUrl, ignoreCase = true)) return false
        if (!normalizeServerUrl(currentHostname).equals(primaryUrl, ignoreCase = true)) return false

        selectedHardcodedServerUrl = fallbackUrl
        viewModelScope.launch {
            refreshServer()
        }
        return true
    }

    private suspend fun refreshServer() {
        val serverEntity = if (hardcodedServerEnabled) {
            val hardcodedUrl = selectedHardcodedServerUrl
                ?: hardcodedServerUrl.takeIf(String::isNotEmpty)
                ?: hardcodedFallbackServerUrl
            apiClientController.setupServer(hardcodedUrl)
        } else {
            apiClientController.loadSavedServer()
        }
        if (serverEntity == null) {
            _serverState.value = if (hardcodedServerEnabled) ServerState.Pending else ServerState.Unset
            return
        }

        val savedUser = apiClientController.loadSavedServerUser()
        if (savedUser?.accessToken != null) {
            val expiryStatus = apiClientController.getUserExpiryStatus(serverEntity, savedUser.accessToken)
            if (expiryStatus.isExpired) {
                _serverState.value = ServerState.Expired(serverEntity, expiryStatus.expiryDateRaw)
                return
            }
        }

        _serverState.value = ServerState.Available(serverEntity)
    }

    private fun normalizeServerUrl(url: String): String = url.trim().trimEnd('/')

    /**
     * Temporarily unset the selected server to be able to connect to a different one
     */
    fun resetServer() {
        if (hardcodedServerEnabled) {
            viewModelScope.launch {
                refreshServer()
            }
            return
        }

        _serverState.value = ServerState.Unset
    }
}

sealed class ServerState {
    open val server: ServerEntity? = null

    object Pending : ServerState()
    object Unset : ServerState()
    class Available(override val server: ServerEntity) : ServerState()
    class Expired(override val server: ServerEntity, val expiryDate: String?) : ServerState()
}
