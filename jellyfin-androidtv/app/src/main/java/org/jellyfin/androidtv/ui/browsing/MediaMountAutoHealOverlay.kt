package org.jellyfin.androidtv.ui.browsing

import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import org.jellyfin.androidtv.R
import org.jellyfin.androidtv.data.service.MediaMountAutoHealState
import org.jellyfin.androidtv.data.service.MediaMountAutoHealStatusService
import org.jellyfin.androidtv.ui.base.Icon
import org.jellyfin.androidtv.ui.base.Text
import org.koin.compose.koinInject

@Composable
fun MediaMountAutoHealOverlay() {
	val statusService = koinInject<MediaMountAutoHealStatusService>()
	val status by statusService.status.collectAsState()
	val activeStatus = status ?: return

	val backgroundColor = when (activeStatus.state) {
		MediaMountAutoHealState.RECONNECTING -> Color(0xCC1D4ED8)
		MediaMountAutoHealState.RECOVERED -> Color(0xCC047857)
		MediaMountAutoHealState.DEGRADED -> Color(0xCCB45309)
		MediaMountAutoHealState.HEALTHY -> Color.Transparent
	}
	val borderColor = when (activeStatus.state) {
		MediaMountAutoHealState.RECONNECTING -> Color(0xFF60A5FA)
		MediaMountAutoHealState.RECOVERED -> Color(0xFF34D399)
		MediaMountAutoHealState.DEGRADED -> Color(0xFFF59E0B)
		MediaMountAutoHealState.HEALTHY -> Color.Transparent
	}
	val iconResource = when (activeStatus.state) {
		MediaMountAutoHealState.RECONNECTING -> R.drawable.ic_more
		MediaMountAutoHealState.RECOVERED -> R.drawable.ic_check
		MediaMountAutoHealState.DEGRADED -> R.drawable.ic_error
		MediaMountAutoHealState.HEALTHY -> R.drawable.ic_error
	}
	val supportLine = when (activeStatus.state) {
		MediaMountAutoHealState.RECONNECTING -> "Playback requests may fail briefly while storage reconnects."
		MediaMountAutoHealState.RECOVERED -> "The media path is available again and playback can be retried now."
		MediaMountAutoHealState.DEGRADED -> "Playback is still blocked because the media path has not recovered yet."
		MediaMountAutoHealState.HEALTHY -> ""
	}

	Box(
		modifier = Modifier
			.fillMaxSize()
			.padding(top = 24.dp, start = 28.dp, end = 28.dp),
		contentAlignment = Alignment.TopCenter
	) {
		Row(
			modifier = Modifier
				.fillMaxWidth()
				.background(color = backgroundColor, shape = RoundedCornerShape(10.dp))
				.border(width = 2.dp, color = borderColor, shape = RoundedCornerShape(10.dp))
				.padding(horizontal = 18.dp, vertical = 14.dp),
			verticalAlignment = Alignment.Top,
			horizontalArrangement = Arrangement.spacedBy(12.dp)
		) {
			Icon(
				painter = painterResource(id = iconResource),
				contentDescription = null,
				modifier = Modifier
					.size(22.dp)
					.padding(top = 2.dp),
				tint = Color.White
			)

			Column(verticalArrangement = Arrangement.spacedBy(4.dp)) {
				Text(
					text = activeStatus.message,
					color = Color.White,
					fontWeight = FontWeight.Bold
				)
				Text(
					text = supportLine,
					color = Color.White.copy(alpha = 0.92f)
				)
				activeStatus.detail?.takeIf { activeStatus.state == MediaMountAutoHealState.DEGRADED }?.let { detail ->
					Text(
						text = detail,
						color = Color.White.copy(alpha = 0.86f)
					)
				}
			}
		}
	}
}
