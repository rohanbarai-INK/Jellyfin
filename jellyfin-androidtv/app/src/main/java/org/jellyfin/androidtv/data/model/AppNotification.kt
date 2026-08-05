package org.jellyfin.androidtv.data.model

data class AppNotification(
	val message: String,
	val actions: List<AppNotificationAction> = emptyList(),
	val dismiss: () -> Unit,
	val public: Boolean,
)

data class AppNotificationAction(
	val label: String,
	val onClick: () -> Unit,
)
