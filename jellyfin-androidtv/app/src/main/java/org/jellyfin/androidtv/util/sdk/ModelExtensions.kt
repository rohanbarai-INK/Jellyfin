@file:JvmName("ModelUtils")

package org.jellyfin.androidtv.util.sdk

import org.jellyfin.androidtv.auth.model.PublicUser
import org.jellyfin.androidtv.auth.model.Server
import org.jellyfin.androidtv.util.apiclient.primaryImage
import org.jellyfin.sdk.model.api.ServerDiscoveryInfo
import org.jellyfin.sdk.model.api.UserDto
import org.jellyfin.sdk.model.serializer.toUUID
import org.jellyfin.sdk.model.serializer.toUUIDOrNull
import timber.log.Timber

fun ServerDiscoveryInfo.toServer(): Server = Server(
	id = id.toUUID(),
	name = name,
	address = address,
)

fun UserDto.toPublicUser(): PublicUser? {
	return PublicUser(
		id = id,
		name = name ?: return null,
		serverId = serverId?.toUUIDOrNull() ?: return null,
		accessToken = null,
		imageTag = primaryImage?.tag,
		expiryDate = expiryDateRaw()
	)
}

fun UserDto.expiryDateRaw(): String? {
	val value = runCatching {
		javaClass.methods
			.firstOrNull { method ->
				method.name == "getExpiryDate" &&
					method.parameterCount == 0
			}
			?.invoke(this)
	}.onFailure { error ->
		Timber.v(error, "Failed to read UserDto expiry date")
	}.getOrNull() ?: return null

	return value.toString().takeUnless { it.isBlank() || it == "null" }
}
