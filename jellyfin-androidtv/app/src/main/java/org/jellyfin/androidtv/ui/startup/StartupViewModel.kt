package org.jellyfin.androidtv.ui.startup

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.firstOrNull
import kotlinx.coroutines.launch
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import org.jellyfin.androidtv.BuildConfig
import org.jellyfin.androidtv.auth.model.AuthenticationSortBy
import org.jellyfin.androidtv.auth.model.AutomaticAuthenticateMethod
import org.jellyfin.androidtv.auth.model.ConnectedState
import org.jellyfin.androidtv.auth.model.LoginState
import org.jellyfin.androidtv.auth.model.PrivateUser
import org.jellyfin.androidtv.auth.model.Server
import org.jellyfin.androidtv.auth.model.User
import org.jellyfin.androidtv.auth.repository.AuthenticationRepository
import org.jellyfin.androidtv.auth.repository.ServerRepository
import org.jellyfin.androidtv.auth.repository.ServerUserRepository
import org.jellyfin.androidtv.auth.store.AuthenticationPreferences
import java.util.UUID

class StartupViewModel(
	private val serverRepository: ServerRepository,
	private val serverUserRepository: ServerUserRepository,
	private val authenticationRepository: AuthenticationRepository,
	private val authenticationPreferences: AuthenticationPreferences,
) : ViewModel() {
	val storedServers = serverRepository.storedServers
	val discoveredServers = serverRepository.discoveredServers

	private val _users = MutableStateFlow<List<User>>(emptyList())
	val users = _users.asStateFlow()

	private val userComparator = compareByDescending<User> { user ->
		if (
			authenticationPreferences[AuthenticationPreferences.sortBy] == AuthenticationSortBy.LAST_USE &&
			user is PrivateUser
		) user.lastUsed
		else null
	}.thenBy { user -> user.name }

	private val discoveryMutex = Mutex()

	private fun normalizeServerAddress(address: String): String {
		return address.trim().trimEnd('/')
	}

	fun getHardcodedServerUrl(): String = normalizeServerAddress(BuildConfig.HARDCODED_SERVER_URL)

	private fun getHardcodedFallbackServerUrl(): String = normalizeServerAddress(BuildConfig.HARDCODED_FALLBACK_SERVER_URL)

	private fun getHardcodedServerUrls(): List<String> = listOf(
		getHardcodedServerUrl(),
		getHardcodedFallbackServerUrl()
	).filter(String::isNotBlank).distinctBy(String::lowercase)

	fun isHardcodedServerModeEnabled(): Boolean = getHardcodedServerUrls().isNotEmpty()

	fun getServer(id: UUID) = serverRepository.storedServers.value
		.find { it.id == id }

	fun loadUsers(server: Server) {
		viewModelScope.launch {
			val storedUsers = serverUserRepository.getStoredServerUsers(server)
			_users.value = storedUsers.sortedWith(userComparator)

			val storedUserIds = storedUsers.map { it.id }
			val publicUsers = serverUserRepository.getPublicServerUsers(server)
				.filterNot { it.id in storedUserIds }
			_users.value = (storedUsers + publicUsers).sortedWith(userComparator)
		}
	}

	fun addServer(address: String) = serverRepository.addServer(address)

	fun deleteServer(serverId: UUID) {
		viewModelScope.launch { serverRepository.deleteServer(serverId) }
	}

	fun authenticate(server: Server, user: User): Flow<LoginState> =
		authenticationRepository.authenticate(server, AutomaticAuthenticateMethod(user))

	fun getUserImage(server: Server, user: User): String? =
		authenticationRepository.getUserImageUrl(server, user)

	fun loadDiscoveryServers() {
		// Only run one discovery process at a time
		if (discoveryMutex.isLocked) return

		viewModelScope.launch(Dispatchers.IO) {
			discoveryMutex.withLock {
				serverRepository.loadDiscoveryServers()
			}
		}
	}

	fun reloadStoredServers() {
		viewModelScope.launch { serverRepository.loadStoredServers() }
	}

	suspend fun getLastServer(): Server? {
		serverRepository.loadStoredServers()
		return serverRepository.storedServers.value.maxByOrNull { it.dateLastAccessed }
	}

	suspend fun updateServer(server: Server): Boolean = serverRepository.updateServer(server)

	suspend fun resolveHardcodedServer(): Server? {
		serverRepository.loadStoredServers()
		for (hardcodedAddress in getHardcodedServerUrls()) {
			val existingServer = serverRepository.storedServers.value.firstOrNull { server ->
				normalizeServerAddress(server.address).equals(hardcodedAddress, ignoreCase = true)
			}
			if (existingServer != null && serverRepository.updateServer(existingServer, force = true)) {
				return serverRepository.getServer(existingServer.id) ?: existingServer
			}

			val connectedState = serverRepository.addServer(hardcodedAddress)
				.firstOrNull { state -> state is ConnectedState } as? ConnectedState
				?: continue

			return serverRepository.getServer(connectedState.id)
				?: serverRepository.storedServers.value.firstOrNull { server -> server.id == connectedState.id }
		}

		return null
	}
}
