package org.jellyfin.androidtv.ui.notification

import android.view.LayoutInflater
import android.view.ViewGroup
import android.widget.Button
import androidx.leanback.widget.Presenter
import org.jellyfin.androidtv.data.model.AppNotification
import org.jellyfin.androidtv.databinding.ViewCardNotificationBinding

class AppNotificationPresenter : Presenter() {
	override fun onCreateViewHolder(parent: ViewGroup): ViewHolder {
		val binding = ViewCardNotificationBinding.inflate(LayoutInflater.from(parent.context), parent, false)
		return AppNotificationViewHolder(binding)
	}

	override fun onBindViewHolder(viewHolder: ViewHolder, item: Any?) {
		viewHolder as AppNotificationViewHolder
		item as AppNotification

		viewHolder.binding.message.text = item.message
		viewHolder.binding.actionsContainer.removeAllViews()

		item.actions.forEach { action ->
			val button = Button(viewHolder.binding.root.context).apply {
				text = action.label
				isFocusable = true
				isFocusableInTouchMode = true
				setOnClickListener { action.onClick() }
			}
			viewHolder.binding.actionsContainer.addView(button)
		}
	}

	override fun onUnbindViewHolder(viewHolder: ViewHolder) {
		viewHolder as AppNotificationViewHolder
	}

	private class AppNotificationViewHolder(val binding: ViewCardNotificationBinding) : ViewHolder(binding.root)
}
