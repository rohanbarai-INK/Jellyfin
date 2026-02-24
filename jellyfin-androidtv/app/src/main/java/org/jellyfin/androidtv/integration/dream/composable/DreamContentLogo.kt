package org.jellyfin.androidtv.integration.dream.composable

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.fillMaxHeight
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.width
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.unit.dp
import org.jellyfin.androidtv.R
import org.jellyfin.androidtv.ui.shared.branding.ServerLogo

@Composable
fun DreamContentLogo() = Box(
	modifier = Modifier
		.fillMaxSize()
		.background(Color.Black),
) {
	ServerLogo(
		contentDescription = stringResource(R.string.app_name),
		maxImageSizePx = 1200,
		modifier = Modifier
			.align(Alignment.Center)
			.width(400.dp)
			.fillMaxHeight(),
	)
}
