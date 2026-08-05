package org.jellyfin.androidtv.ui.shared.toolbar

import android.os.Build
import android.content.Context
import android.content.Intent
import android.net.Uri
import android.provider.Settings
import android.widget.Toast
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableIntStateOf
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.focus.FocusRequester
import androidx.compose.ui.focus.focusRequester
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.res.vectorResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.core.content.FileProvider
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import org.jellyfin.androidtv.R
import org.jellyfin.androidtv.auth.repository.SessionRepository
import org.jellyfin.androidtv.auth.store.AuthenticationStore
import org.jellyfin.androidtv.ui.base.Badge
import org.jellyfin.androidtv.ui.base.Icon
import org.jellyfin.androidtv.ui.base.JellyfinTheme
import org.jellyfin.androidtv.ui.base.Text
import org.jellyfin.androidtv.ui.base.button.Button
import org.jellyfin.androidtv.ui.base.button.ButtonDefaults
import org.jellyfin.androidtv.ui.base.button.IconButton
import org.jellyfin.androidtv.ui.base.dialog.DialogBase
import org.koin.compose.koinInject
import org.json.JSONObject
import java.io.File
import java.io.FileOutputStream
import java.net.HttpURLConnection
import java.net.URL
import java.net.URLEncoder
import java.nio.charset.StandardCharsets

private const val DOWNLOAD_PREFS_NAME = "app_download_prefs"
private const val DEFAULT_TV_APK_FILE_NAME = "KnightFlixTV-v0.0.1.apk"

private data class ServerContext(
    val baseUrl: String,
    val accessToken: String,
)

private data class AppDownloadConfig(
    val tvApkUrl: String,
    val tvApkFileName: String,
    val tvIsNew: Boolean,
    val maxNewInteractions: Int,
)

private sealed interface AppDownloadConfigState {
    data object Loading : AppDownloadConfigState
    data class Ready(val config: AppDownloadConfig) : AppDownloadConfigState
    data class Error(val message: String) : AppDownloadConfigState
}

private sealed interface DownloadActionState {
    data object Idle : DownloadActionState
    data object Downloading : DownloadActionState
    data class Error(val message: String) : DownloadActionState
}

@Composable
fun DownloadAppToolbarButton(
    modifier: Modifier = Modifier,
) {
    val context = androidx.compose.ui.platform.LocalContext.current
    val scope = rememberCoroutineScope()
    val sessionRepository = koinInject<SessionRepository>()
    val authenticationStore = koinInject<AuthenticationStore>()
    val session by sessionRepository.currentSession.collectAsState()
    val serverContext = remember(session) {
        session?.let { activeSession ->
            authenticationStore.getServer(activeSession.serverId)
                ?.address
                ?.trimEnd('/')
                ?.takeIf { it.isNotBlank() }
                ?.let { baseUrl ->
                    ServerContext(
                        baseUrl = baseUrl,
                        accessToken = activeSession.accessToken,
                    )
                }
        }
    }
    var dialogVisible by rememberSaveable { mutableStateOf(false) }
    var configState by remember(serverContext) { mutableStateOf<AppDownloadConfigState>(AppDownloadConfigState.Loading) }
    var downloadState by remember { mutableStateOf<DownloadActionState>(DownloadActionState.Idle) }
    var tvInteractionCount by remember { mutableIntStateOf(0) }
    var pendingPermissionConfig by remember { mutableStateOf<AppDownloadConfig?>(null) }
    val unknownAppsLauncher = rememberLauncherForActivityResult(ActivityResultContracts.StartActivityForResult()) {
        val pendingConfig = pendingPermissionConfig ?: return@rememberLauncherForActivityResult
        if (canInstallPackages(context)) {
            scope.launch {
                downloadState = DownloadActionState.Idle
                startDownloadAndInstall(
                    context = context,
                    config = pendingConfig,
                    onInteractionCountChanged = { tvInteractionCount = it },
                    onStateChanged = { downloadState = it },
                    onDismiss = { dialogVisible = false },
                )
            }
        }
        pendingPermissionConfig = null
    }

    LaunchedEffect(serverContext) {
        if (serverContext == null) {
            configState = AppDownloadConfigState.Error(context.getString(R.string.download_app_config_unavailable))
            return@LaunchedEffect
        }

        configState = AppDownloadConfigState.Loading
        configState = runCatching {
            withContext(Dispatchers.IO) {
                fetchAppDownloadConfig(serverContext)
            }
        }.fold(
            onSuccess = { config ->
                tvInteractionCount = getInteractionCount(context, config.tvApkUrl)
                AppDownloadConfigState.Ready(config)
            },
            onFailure = { throwable ->
                AppDownloadConfigState.Error(
                    throwable.message?.takeIf { it.isNotBlank() }
                        ?: context.getString(R.string.download_app_config_error)
                )
            }
        )
    }

    val showNewBadge = remember(configState, tvInteractionCount) {
        (configState as? AppDownloadConfigState.Ready)?.config?.let { config ->
            config.tvIsNew && tvInteractionCount < config.maxNewInteractions
        } ?: false
    }

    Box(modifier = modifier) {
        IconButton(
            onClick = { dialogVisible = true },
        ) {
            Icon(
                imageVector = ImageVector.vectorResource(R.drawable.ic_download),
                contentDescription = stringResource(R.string.download_app_toolbar_action),
            )
        }

        if (showNewBadge) {
            Badge(
                modifier = Modifier
                    .align(Alignment.TopEnd)
            ) {
                Text(text = stringResource(R.string.lbl_new))
            }
        }
    }

    DownloadAppDialog(
        visible = dialogVisible,
        onDismissRequest = {
            dialogVisible = false
            downloadState = DownloadActionState.Idle
        },
        configState = configState,
        downloadState = downloadState,
        showNewBadge = showNewBadge,
        onDownloadClick = {
            val config = (configState as? AppDownloadConfigState.Ready)?.config ?: return@DownloadAppDialog
            if (config.tvApkUrl.isBlank()) {
                downloadState = DownloadActionState.Error(context.getString(R.string.download_app_config_unavailable))
                return@DownloadAppDialog
            }

            if (!canInstallPackages(context)) {
                pendingPermissionConfig = config
                downloadState = DownloadActionState.Error(context.getString(R.string.download_app_unknown_sources_required))
                if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
                    unknownAppsLauncher.launch(
                        Intent(
                            Settings.ACTION_MANAGE_UNKNOWN_APP_SOURCES,
                            Uri.parse("package:${context.packageName}")
                        )
                    )
                } else {
                    unknownAppsLauncher.launch(
                        Intent(
                            Settings.ACTION_APPLICATION_SETTINGS,
                            Uri.parse("package:${context.packageName}")
                        )
                    )
                }
                return@DownloadAppDialog
            }

            scope.launch {
                startDownloadAndInstall(
                    context = context,
                    config = config,
                    onInteractionCountChanged = { tvInteractionCount = it },
                    onStateChanged = { downloadState = it },
                    onDismiss = { dialogVisible = false },
                )
            }
        }
    )
}

@Composable
private fun DownloadAppDialog(
    visible: Boolean,
    onDismissRequest: () -> Unit,
    configState: AppDownloadConfigState,
    downloadState: DownloadActionState,
    showNewBadge: Boolean,
    onDownloadClick: () -> Unit,
) {
    val primaryFocusRequester = remember { FocusRequester() }

    LaunchedEffect(visible, configState) {
        if (visible && configState is AppDownloadConfigState.Ready) {
            primaryFocusRequester.requestFocus()
        }
    }

    DialogBase(
        visible = visible,
        onDismissRequest = onDismissRequest,
    ) {
        Column(
            modifier = Modifier
                .fillMaxWidth(0.54f)
                .background(
                    color = JellyfinTheme.colorScheme.surface,
                    shape = RoundedCornerShape(28.dp),
                )
                .padding(28.dp),
            verticalArrangement = Arrangement.spacedBy(18.dp),
        ) {
            Row(
                verticalAlignment = Alignment.CenterVertically,
                horizontalArrangement = Arrangement.spacedBy(14.dp),
            ) {
                Box(
                    modifier = Modifier
                        .size(56.dp)
                        .background(
                            color = JellyfinTheme.colorScheme.buttonActive,
                            shape = RoundedCornerShape(18.dp),
                        ),
                    contentAlignment = Alignment.Center,
                ) {
                    Icon(
                        imageVector = ImageVector.vectorResource(R.drawable.ic_download),
                        contentDescription = null,
                    )
                }

                Column(
                    verticalArrangement = Arrangement.spacedBy(4.dp),
                ) {
                    Text(
                        text = stringResource(R.string.download_app_dialog_title),
                        fontWeight = FontWeight.Bold,
                    )
                    Text(
                        text = stringResource(R.string.download_app_dialog_subtitle),
                        color = JellyfinTheme.colorScheme.listCaption,
                    )
                }
            }

            when (configState) {
                AppDownloadConfigState.Loading -> {
                    Text(
                        text = stringResource(R.string.download_app_loading),
                        color = JellyfinTheme.colorScheme.listCaption,
                    )
                }
                is AppDownloadConfigState.Error -> {
                    Text(
                        text = configState.message,
                        color = JellyfinTheme.colorScheme.recording,
                    )
                }
                is AppDownloadConfigState.Ready -> {
                    Text(
                        text = stringResource(R.string.download_app_dialog_body),
                        color = JellyfinTheme.colorScheme.listCaption,
                    )
                }
            }

            if (downloadState is DownloadActionState.Downloading) {
                Text(
                    text = stringResource(R.string.download_app_downloading),
                    color = JellyfinTheme.colorScheme.badge,
                )
            }

            if (downloadState is DownloadActionState.Error) {
                Text(
                    text = downloadState.message,
                    color = JellyfinTheme.colorScheme.recording,
                )
            }

            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.End,
                verticalAlignment = Alignment.CenterVertically,
            ) {
                if (showNewBadge) {
                    Badge {
                        Text(text = stringResource(R.string.lbl_new))
                    }
                    Spacer(Modifier.width(12.dp))
                }

                Button(
                    onClick = onDismissRequest,
                ) {
                    Text(text = stringResource(R.string.btn_cancel))
                }

                Spacer(Modifier.width(12.dp))

                Button(
                    onClick = onDownloadClick,
                    modifier = Modifier.focusRequester(primaryFocusRequester),
                    enabled = configState is AppDownloadConfigState.Ready && downloadState !is DownloadActionState.Downloading,
                    colors = ButtonDefaults.colors(
                        containerColor = JellyfinTheme.colorScheme.buttonActive,
                        contentColor = JellyfinTheme.colorScheme.onButtonActive,
                        focusedContainerColor = JellyfinTheme.colorScheme.badge,
                        focusedContentColor = JellyfinTheme.colorScheme.onBadge,
                    ),
                ) {
                    Text(text = stringResource(R.string.download_app_action))
                }
            }
        }
    }
}

private suspend fun startDownloadAndInstall(
    context: Context,
    config: AppDownloadConfig,
    onInteractionCountChanged: (Int) -> Unit,
    onStateChanged: (DownloadActionState) -> Unit,
    onDismiss: () -> Unit,
) {
    onStateChanged(DownloadActionState.Downloading)
    val interactionCount = incrementInteractionCount(context, config.tvApkUrl)
    onInteractionCountChanged(interactionCount)

    runCatching {
        withContext(Dispatchers.IO) {
            downloadApk(context, config.tvApkUrl, config.tvApkFileName)
        }
    }.fold(
        onSuccess = { apkFile ->
            runCatching { openPackageInstaller(context, apkFile) }
                .onSuccess {
                    onStateChanged(DownloadActionState.Idle)
                    onDismiss()
                }
                .onFailure { throwable ->
                    onStateChanged(
                        DownloadActionState.Error(
                            throwable.message?.takeIf { it.isNotBlank() }
                                ?: context.getString(R.string.download_app_install_error)
                        )
                    )
                }
        },
        onFailure = { throwable ->
            val message = throwable.message?.takeIf { it.isNotBlank() }
                ?: context.getString(R.string.download_app_download_error)
            onStateChanged(DownloadActionState.Error(message))
            Toast.makeText(context, message, Toast.LENGTH_LONG).show()
        }
    )
}

private fun fetchAppDownloadConfig(serverContext: ServerContext): AppDownloadConfig {
    val endpoint = "${serverContext.baseUrl}/AppDownload/Config?api_key=${
        URLEncoder.encode(serverContext.accessToken, StandardCharsets.UTF_8.name())
    }"
    val connection = URL(endpoint).openConnection() as HttpURLConnection
    connection.requestMethod = "GET"
    connection.connectTimeout = 10000
    connection.readTimeout = 10000
    connection.setRequestProperty("Accept", "application/json")

    return try {
        val statusCode = connection.responseCode
        val body = ((if (statusCode in 200..299) connection.inputStream else connection.errorStream)
            ?.bufferedReader()
            ?.use { it.readText() }).orEmpty()

        if (statusCode !in 200..299) {
            throw IllegalStateException(body.ifBlank { "HTTP $statusCode" })
        }

        val payload = JSONObject(body)
        AppDownloadConfig(
            tvApkUrl = payload.optStringAny("TvApkUrl", "tvApkUrl"),
            tvApkFileName = payload.optStringAny("TvApkFileName", "tvApkFileName").ifBlank { DEFAULT_TV_APK_FILE_NAME },
            tvIsNew = payload.optBooleanAny("TvIsNew", "tvIsNew"),
            maxNewInteractions = payload.optIntAny("MaxNewInteractions", "maxNewInteractions").coerceAtLeast(1).takeIf { it > 0 } ?: 3,
        )
    } finally {
        connection.disconnect()
    }
}

private fun downloadApk(
    context: Context,
    url: String,
    fileName: String,
): File {
    val connection = URL(url).openConnection() as HttpURLConnection
    connection.requestMethod = "GET"
    connection.connectTimeout = 15000
    connection.readTimeout = 60000

    return try {
        val statusCode = connection.responseCode
        if (statusCode !in 200..299) {
            throw IllegalStateException("HTTP $statusCode")
        }

        val downloadsDir = context.cacheDir.resolve("app-downloads").apply { mkdirs() }
        val targetFile = downloadsDir.resolve(sanitizeApkFileName(fileName))
        if (targetFile.exists()) targetFile.delete()

        connection.inputStream.use { input ->
            FileOutputStream(targetFile).use { output ->
                input.copyTo(output)
            }
        }

        targetFile
    } finally {
        connection.disconnect()
    }
}

private fun openPackageInstaller(context: Context, apkFile: File) {
    val uri = FileProvider.getUriForFile(context, "${context.packageName}.fileprovider", apkFile)
    val intent = Intent(Intent.ACTION_VIEW).apply {
        setDataAndType(uri, "application/vnd.android.package-archive")
        addFlags(Intent.FLAG_GRANT_READ_URI_PERMISSION)
        addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
    }
    context.startActivity(intent)
}

private fun canInstallPackages(context: Context): Boolean {
    return Build.VERSION.SDK_INT < Build.VERSION_CODES.O || context.packageManager.canRequestPackageInstalls()
}

private fun sanitizeApkFileName(fileName: String): String {
    val sanitized = fileName
        .ifBlank { DEFAULT_TV_APK_FILE_NAME }
        .replace(Regex("[^a-zA-Z0-9._-]"), "_")

    return if (sanitized.endsWith(".apk", ignoreCase = true)) sanitized else "$sanitized.apk"
}

private fun getInteractionCount(context: Context, apkUrl: String): Int {
	return context
		.getSharedPreferences(DOWNLOAD_PREFS_NAME, Context.MODE_PRIVATE)
		.getInt(interactionKey(apkUrl), 0)
}

private fun incrementInteractionCount(context: Context, apkUrl: String): Int {
	val prefs = context.getSharedPreferences(DOWNLOAD_PREFS_NAME, Context.MODE_PRIVATE)
	val next = prefs.getInt(interactionKey(apkUrl), 0) + 1
	prefs.edit().putInt(interactionKey(apkUrl), next).apply()
	return next
}

private fun interactionKey(apkUrl: String): String {
	return buildString {
		append("tv_app_download_")
		append(apkUrl.filter(Char::isLetterOrDigit).take(48))
	}
}

private fun JSONObject.optStringAny(vararg keys: String): String {
	for (key in keys) {
		val value = optString(key)
		if (value.isNotBlank()) return value
	}
	return ""
}

private fun JSONObject.optBooleanAny(vararg keys: String): Boolean {
	for (key in keys) {
		if (has(key)) return optBoolean(key)
	}
	return false
}

private fun JSONObject.optIntAny(vararg keys: String): Int {
	for (key in keys) {
		if (has(key)) return optInt(key)
	}
	return 0
}
