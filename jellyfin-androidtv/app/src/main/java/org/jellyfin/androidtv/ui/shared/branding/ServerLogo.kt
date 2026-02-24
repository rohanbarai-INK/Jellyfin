package org.jellyfin.androidtv.ui.shared.branding

import androidx.compose.foundation.Image
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.res.painterResource
import coil3.compose.rememberAsyncImagePainter
import coil3.request.ImageRequest
import coil3.size.Size
import org.jellyfin.androidtv.R
import org.jellyfin.androidtv.auth.repository.ServerRepository
import org.koin.compose.koinInject

@Composable
private fun rememberServerLogoUrl(): String? {
    val serverRepository = koinInject<ServerRepository>()
    val server by serverRepository.currentServer.collectAsState()

    return remember(server?.address) {
        server?.address
            ?.trimEnd('/')
            ?.let { baseUrl -> "$baseUrl/Branding/Logo?t=${System.currentTimeMillis()}" }
    }
}

@Composable
fun ServerLogo(
    modifier: Modifier = Modifier,
    contentDescription: String? = null,
    maxImageSizePx: Int = 1024,
) {
    val context = LocalContext.current
    val logoUrl = rememberServerLogoUrl()
    val fallbackPainter = painterResource(R.drawable.app_logo)

    if (logoUrl == null) {
        Image(
            painter = fallbackPainter,
            contentDescription = contentDescription,
            modifier = modifier,
        )
        return
    }

    val imageRequest = remember(logoUrl, maxImageSizePx) {
        ImageRequest.Builder(context)
            .data(logoUrl)
            .size(Size(maxImageSizePx, maxImageSizePx))
            .build()
    }

    val logoPainter = rememberAsyncImagePainter(
        model = imageRequest,
        placeholder = fallbackPainter,
        error = fallbackPainter,
        fallback = fallbackPainter,
    )

    Image(
        painter = logoPainter,
        contentDescription = contentDescription,
        modifier = modifier,
    )
}

