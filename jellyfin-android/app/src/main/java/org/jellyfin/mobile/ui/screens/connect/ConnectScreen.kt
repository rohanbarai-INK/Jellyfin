package org.jellyfin.mobile.ui.screens.connect

import androidx.compose.foundation.Image
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.systemBarsPadding
import androidx.compose.material.ButtonDefaults
import androidx.compose.material.MaterialTheme
import androidx.compose.material.Surface
import androidx.compose.material.Text
import androidx.compose.material.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.Stable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.unit.dp
import coil3.compose.AsyncImage
import coil3.request.ImageRequest
import org.jellyfin.mobile.MainViewModel
import org.jellyfin.mobile.R
import org.jellyfin.mobile.app.ApiClientController
import org.jellyfin.mobile.events.ActivityEvent
import org.jellyfin.mobile.events.ActivityEventHandler
import org.jellyfin.mobile.ui.utils.CenterRow
import org.koin.compose.koinInject

@Composable
fun ConnectScreen(
    mainViewModel: MainViewModel,
    showExternalConnectionError: Boolean,
    apiClientController: ApiClientController = koinInject(),
    activityEventHandler: ActivityEventHandler = koinInject(),
) {
    var logoBaseUrl by remember { mutableStateOf<String?>(null) }

    LaunchedEffect(apiClientController) {
        logoBaseUrl = apiClientController.loadSavedServer()?.hostname
    }

    Surface(color = MaterialTheme.colors.background) {
        Column(
            modifier = Modifier
                .fillMaxSize()
                .systemBarsPadding()
                .padding(horizontal = 16.dp),
        ) {
            LogoHeader(logoBaseUrl)
            ServerSelection(
                showExternalConnectionError = showExternalConnectionError,
                onConnected = { hostname ->
                    logoBaseUrl = hostname
                    mainViewModel.switchServer(hostname)
                },
            )
            StyledTextButton(
                onClick = { activityEventHandler.emit(ActivityEvent.OpenDownloads) },
                text = stringResource(R.string.view_downloads),
            )
        }
    }
}

@Stable
@Composable
fun LogoHeader(logoBaseUrl: String?) {
    val context = LocalContext.current
    val logoUrl = remember(logoBaseUrl) {
        logoBaseUrl?.trimEnd('/')?.let { baseUrl ->
            "$baseUrl/Branding/Logo?t=${System.currentTimeMillis()}"
        }
    }

    CenterRow(
        modifier = Modifier.padding(vertical = 25.dp),
    ) {
        if (logoUrl == null) {
            Image(
                painter = painterResource(R.drawable.app_logo),
                modifier = Modifier
                    .height(72.dp),
                contentDescription = null,
            )
        } else {
            AsyncImage(
                model = ImageRequest.Builder(context)
                    .data(logoUrl)
                    .build(),
                placeholder = painterResource(R.drawable.app_logo),
                error = painterResource(R.drawable.app_logo),
                fallback = painterResource(R.drawable.app_logo),
                modifier = Modifier
                    .height(72.dp),
                contentDescription = null,
            )
        }
    }
}

@Stable
@Composable
fun StyledTextButton(
    text: String,
    enabled: Boolean = true,
    onClick: () -> Unit,
) {
    TextButton(
        onClick = onClick,
        modifier = Modifier
            .fillMaxWidth()
            .padding(vertical = 4.dp),
        enabled = enabled,
        colors = ButtonDefaults.buttonColors(),
    ) {
        Text(text = text)
    }
}
