package org.jellyfin.androidtv.ui.subscription

import android.content.Intent
import android.os.Bundle
import androidx.activity.compose.BackHandler
import androidx.appcompat.app.AppCompatActivity
import androidx.compose.animation.animateColorAsState
import androidx.compose.animation.core.animateFloatAsState
import androidx.compose.animation.core.tween
import androidx.compose.foundation.ExperimentalFoundationApi
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.combinedClickable
import androidx.compose.foundation.interaction.MutableInteractionSource
import androidx.compose.foundation.interaction.collectIsFocusedAsState
import androidx.compose.foundation.interaction.collectIsPressedAsState
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ColumnScope
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxHeight
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.rememberLazyListState
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.text.BasicTextField
import androidx.compose.foundation.text.KeyboardActions
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.runtime.Composable
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.draw.shadow
import androidx.compose.ui.focus.FocusRequester
import androidx.compose.ui.focus.focusRequester
import androidx.compose.ui.graphics.Brush
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.SolidColor
import androidx.compose.ui.graphics.graphicsLayer
import androidx.compose.ui.res.pluralStringResource
import androidx.compose.ui.res.stringResource
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.style.TextDecoration
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.lifecycle.lifecycleScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import org.jellyfin.androidtv.R
import org.jellyfin.androidtv.auth.model.UserAccessState
import org.jellyfin.androidtv.auth.model.fetchUserAccessState
import org.jellyfin.androidtv.auth.model.isUserExpired
import org.jellyfin.androidtv.auth.repository.SessionRepository
import org.jellyfin.androidtv.auth.repository.UserRepository
import org.jellyfin.androidtv.auth.store.AuthenticationStore
import org.jellyfin.androidtv.databinding.ActivitySubscriptionManagementBinding
import org.jellyfin.androidtv.ui.background.AppBackground
import org.jellyfin.androidtv.ui.base.JellyfinTheme
import org.jellyfin.androidtv.ui.base.ProvideTextStyle
import org.jellyfin.androidtv.ui.base.Text
import org.jellyfin.androidtv.ui.base.button.Button
import org.jellyfin.androidtv.ui.base.form.Checkbox
import org.jellyfin.androidtv.ui.startup.StartupActivity
import org.jellyfin.androidtv.ui.startup.SubscriptionExpiredActivity
import org.jellyfin.androidtv.util.applyTheme
import org.jellyfin.androidtv.util.sdk.expiryDateRaw
import org.jellyfin.design.Tokens
import org.json.JSONArray
import org.json.JSONException
import org.json.JSONObject
import org.json.JSONTokener
import org.koin.android.ext.android.inject
import timber.log.Timber
import java.io.IOException
import java.math.RoundingMode
import java.net.HttpURLConnection
import java.net.URL
import java.net.URLEncoder
import java.nio.charset.StandardCharsets
import java.text.DecimalFormat
import java.time.Instant
import java.time.OffsetDateTime
import java.time.ZoneId
import java.time.format.DateTimeFormatter
import java.time.format.DateTimeParseException
import java.util.Locale
import java.util.UUID
import kotlin.math.ceil
import kotlin.math.roundToInt

private const val ENDPOINT_CURRENT_SUBSCRIPTION = "Keys/CurrentSubscription"
private const val ENDPOINT_PRICING = "System/Configuration/subscription"
private const val ENDPOINT_REDEEM = "Keys/Redeem"
private const val AUTO_RENEW_PREFS = "subscription_visual"
private const val DAY_IN_MILLIS = 24L * 60L * 60L * 1000L
private const val RENEW_SCROLL_TARGET_INDEX = 2

private val PLAN_DURATION_DAYS = mapOf(
	1 to 30,
	3 to 90,
	6 to 180,
	12 to 365,
)

private val SUPPORTED_MONTHS = PLAN_DURATION_DAYS.keys

private val PLANS = listOf(
	PlanUiState(
		months = 1,
		titleRes = R.string.subscription_plan_starter,
		descriptionRes = R.string.subscription_plan_starter_desc,
	),
	PlanUiState(
		months = 3,
		titleRes = R.string.subscription_plan_standard,
		descriptionRes = R.string.subscription_plan_standard_desc,
	),
	PlanUiState(
		months = 6,
		titleRes = R.string.subscription_plan_pro,
		descriptionRes = R.string.subscription_plan_pro_desc,
		isPopular = true,
	),
	PlanUiState(
		months = 12,
		titleRes = R.string.subscription_plan_annual,
		descriptionRes = R.string.subscription_plan_annual_desc,
	),
)

class SubscriptionManagementActivity : AppCompatActivity() {
	private data class ServerContext(
		val baseUrl: String,
		val accessToken: String,
		val userId: UUID,
	)

	private val sessionRepository: SessionRepository by inject()
	private val userRepository: UserRepository by inject()
	private val authenticationStore: AuthenticationStore by inject()

	private lateinit var binding: ActivitySubscriptionManagementBinding
	private val numberFormatter = DecimalFormat("#.##").apply {
		roundingMode = RoundingMode.HALF_UP
	}
	private val dateFormatter = DateTimeFormatter.ofPattern("dd MMM yyyy", Locale.getDefault())
		.withZone(ZoneId.systemDefault())

	private val _uiState = MutableStateFlow(SubscriptionUiState())
	private val uiState = _uiState.asStateFlow()
	private var serverContext: ServerContext? = null

	override fun onCreate(savedInstanceState: Bundle?) {
		applyTheme()

		super.onCreate(savedInstanceState)
		if (!validateAuthentication()) return

		binding = ActivitySubscriptionManagementBinding.inflate(layoutInflater)
		binding.background.setContent { AppBackground() }
		binding.content.setContent {
			JellyfinTheme {
				val state by uiState.collectAsState()
				SubscriptionManagementScreen(
					uiState = state,
					onAutoRenewToggle = ::updateAutoRenewToggle,
					onAccessKeyChange = ::updateAccessKey,
					onRedeemAccessKey = ::redeemAccessKey,
					onBack = { finishAfterTransition() },
					formatPrice = ::formatPrice,
					formatDate = ::formatDate,
				)
			}
		}
		setContentView(binding.root)

		lifecycleScope.launch {
			initializeScreen()
		}
	}

	override fun onResume() {
		super.onResume()
		applyTheme()
	}

	private fun validateAuthentication(): Boolean {
		if (sessionRepository.currentSession.value == null || userRepository.currentUser.value == null) {
			Timber.w("SubscriptionManagementActivity started without a session, redirecting to StartupActivity")
			startActivity(Intent(this, StartupActivity::class.java))
			finishAfterTransition()
			return false
		}

		return true
	}

	private suspend fun initializeScreen() {
		val context = resolveServerContext()
		if (context == null) {
			_uiState.update {
				it.copy(
					isLoading = false,
					statusErrorMessage = getString(R.string.subscription_missing_session_error),
				)
			}
			return
		}
		serverContext = context

		val currentUser = userRepository.currentUser.value
		val userAccessState = resolveCurrentUserAccessState(context)
		if (isUserExpired(userAccessState)) {
			openSubscriptionExpiredActivity(userAccessState.expiryDateRaw)
			return
		}

		_uiState.update {
			it.copy(
				userName = currentUser?.name.orEmpty(),
				fallbackExpiryDate = parseInstant(userAccessState.expiryDateRaw),
				fallbackIsInGracePeriod = userAccessState.isInGracePeriod,
				fallbackGraceDaysRemaining = userAccessState.graceDaysRemaining,
				autoRenewEnabled = readAutoRenewToggle(context.userId),
			)
		}

		loadSubscriptionData(context)
	}

	private fun resolveServerContext(): ServerContext? {
		val session = sessionRepository.currentSession.value ?: return null
		val server = authenticationStore.getServer(session.serverId) ?: return null

		return ServerContext(
			baseUrl = server.address.trimEnd('/'),
			accessToken = session.accessToken,
			userId = session.userId,
		)
	}

	private suspend fun resolveCurrentUserAccessState(serverContext: ServerContext): UserAccessState {
		val userExpiryDate = userRepository.currentUser.value?.expiryDateRaw()
		return fetchUserAccessState(serverContext.baseUrl, serverContext.accessToken) ?: UserAccessState(
			expiryDateRaw = userExpiryDate,
			isInGracePeriod = false,
			graceDaysRemaining = 0,
		)
	}

	private suspend fun loadSubscriptionData(
		serverContext: ServerContext,
		preserveRedeemMessages: Boolean = false,
	) {
		_uiState.update {
			it.copy(
				isLoading = true,
				statusErrorMessage = null,
				redeemErrorMessage = if (preserveRedeemMessages) it.redeemErrorMessage else null,
				redeemSuccessMessage = if (preserveRedeemMessages) it.redeemSuccessMessage else null,
			)
		}

		val currentResult = withContext(Dispatchers.IO) {
			runCatching {
				fetchCurrentSubscription(serverContext.baseUrl, serverContext.accessToken)
			}
		}
		val pricingResult = withContext(Dispatchers.IO) {
			runCatching {
				fetchSubscriptionPricing(serverContext.baseUrl, serverContext.accessToken)
			}
		}

		val currentSubscription = currentResult.getOrNull()
		val currentSubscriptionError = currentResult.exceptionOrNull()
		val missingSubscriptionHistory =
			currentSubscriptionError is HttpStatusException && currentSubscriptionError.statusCode == 404
		val pricingConfig = pricingResult.getOrNull() ?: PricingConfig()
		val statusErrorMessage = when {
			currentResult.isFailure && pricingResult.isFailure ->
				getString(R.string.subscription_status_and_pricing_load_error)
			pricingResult.isFailure ->
				getString(R.string.subscription_pricing_load_error)
			else -> null
		}

		if (missingSubscriptionHistory) {
			Timber.i("No subscription history returned for current user, using fallback status data")
		} else {
			currentSubscriptionError?.let { Timber.w(it, "Unable to load current subscription metadata") }
		}
		pricingResult.exceptionOrNull()?.let { Timber.w(it, "Unable to load subscription pricing metadata") }

		_uiState.update {
			it.copy(
				isLoading = false,
				statusErrorMessage = statusErrorMessage,
				currentSubscription = currentSubscription,
				pricingConfig = pricingConfig,
				fallbackExpiryDate = currentSubscription?.expiryDate ?: it.fallbackExpiryDate,
				fallbackIsInGracePeriod = currentSubscription?.isInGracePeriod ?: it.fallbackIsInGracePeriod,
				fallbackGraceDaysRemaining = currentSubscription?.graceDaysRemaining ?: it.fallbackGraceDaysRemaining,
			)
		}
	}

	private fun updateAutoRenewToggle(enabled: Boolean) {
		val context = serverContext ?: return
		writeAutoRenewToggle(context.userId, enabled)
		_uiState.update { it.copy(autoRenewEnabled = enabled) }
	}

	private fun updateAccessKey(value: String) {
		_uiState.update { it.copy(accessKey = value) }
	}

	private fun redeemAccessKey() {
		val context = serverContext ?: return
		val key = _uiState.value.accessKey.trim()
		if (key.isBlank()) return

		lifecycleScope.launch {
			_uiState.update {
				it.copy(
					isRedeeming = true,
					redeemErrorMessage = null,
					redeemSuccessMessage = null,
				)
			}

			val redeemResult = withContext(Dispatchers.IO) {
				runCatching {
					redeemAccessKey(context.baseUrl, context.accessToken, key)
				}
			}

			if (redeemResult.isSuccess) {
				_uiState.update {
					it.copy(
						isRedeeming = false,
						accessKey = "",
						redeemSuccessMessage = getString(R.string.subscription_redeem_success),
						redeemErrorMessage = null,
					)
				}
				loadSubscriptionData(context, preserveRedeemMessages = true)
			} else {
				_uiState.update {
					it.copy(
						isRedeeming = false,
						redeemErrorMessage = redeemResult.exceptionOrNull()?.message
							?: getString(R.string.subscription_redeem_error_default),
						redeemSuccessMessage = null,
					)
				}
			}
		}
	}

	private fun readAutoRenewToggle(userId: UUID): Boolean {
		return getSharedPreferences(AUTO_RENEW_PREFS, MODE_PRIVATE)
			.getBoolean(autoRenewPrefKey(userId), false)
	}

	private fun writeAutoRenewToggle(userId: UUID, enabled: Boolean) {
		getSharedPreferences(AUTO_RENEW_PREFS, MODE_PRIVATE)
			.edit()
			.putBoolean(autoRenewPrefKey(userId), enabled)
			.apply()
	}

	private fun autoRenewPrefKey(userId: UUID): String = "jf.subscription.autoRenew.visual.$userId"

	private fun formatDate(instant: Instant?): String {
		return instant?.let { dateFormatter.format(it) } ?: getString(R.string.subscription_not_set)
	}

	private fun formatPrice(value: Double): String {
		if (value % 1.0 == 0.0) {
			return value.toInt().toString()
		}

		return numberFormatter.format(value)
	}

	private fun openSubscriptionExpiredActivity(expiryDate: String?) {
		startActivity(Intent(this, SubscriptionExpiredActivity::class.java).apply {
			putExtra(SubscriptionExpiredActivity.EXTRA_EXPIRY_DATE, expiryDate)
		})
		finishAfterTransition()
	}
}

private data class SubscriptionUiState(
	val userName: String = "",
	val isLoading: Boolean = true,
	val statusErrorMessage: String? = null,
	val currentSubscription: CurrentSubscription? = null,
	val fallbackExpiryDate: Instant? = null,
	val fallbackIsInGracePeriod: Boolean = false,
	val fallbackGraceDaysRemaining: Int = 0,
	val pricingConfig: PricingConfig = PricingConfig(),
	val autoRenewEnabled: Boolean = false,
	val accessKey: String = "",
	val isRedeeming: Boolean = false,
	val redeemErrorMessage: String? = null,
	val redeemSuccessMessage: String? = null,
)

private data class CurrentSubscription(
	val expiryDate: Instant?,
	val status: String,
	val isInGracePeriod: Boolean,
	val graceDaysRemaining: Int,
	val lastDurationMonths: Int?,
)

private data class PricingConfig(
	val gracePeriodDays: Int = 3,
	val basePricePerMonth: Double = 100.0,
	val oneMonthPrice: Double = 100.0,
	val threeMonthPrice: Double = 250.0,
	val sixMonthPrice: Double = 450.0,
	val twelveMonthPrice: Double = 850.0,
)

private data class PlanUiState(
	val months: Int,
	val titleRes: Int,
	val descriptionRes: Int,
	val isPopular: Boolean = false,
)

private data class HttpResponse(
	val statusCode: Int,
	val body: String?,
)

private class HttpStatusException(
	val statusCode: Int,
	message: String,
) : IOException(message)

@Composable
private fun SubscriptionManagementScreen(
	uiState: SubscriptionUiState,
	onAutoRenewToggle: (Boolean) -> Unit,
	onAccessKeyChange: (String) -> Unit,
	onRedeemAccessKey: () -> Unit,
	onBack: () -> Unit,
	formatPrice: (Double) -> String,
	formatDate: (Instant?) -> String,
) {
	BackHandler(onBack = onBack)

	val listState = rememberLazyListState()
	val scope = rememberCoroutineScope()
	val firstPlanFocusRequester = remember { FocusRequester() }

	val subscription = uiState.currentSubscription
	val expiryDate = subscription?.expiryDate ?: uiState.fallbackExpiryDate
	val isInGracePeriod = subscription?.isInGracePeriod ?: uiState.fallbackIsInGracePeriod
	val graceDaysRemaining = (
		subscription?.graceDaysRemaining ?: uiState.fallbackGraceDaysRemaining
	).coerceAtLeast(0)
	val graceDaysTotal = uiState.pricingConfig.gracePeriodDays.coerceAtLeast(0)
	val graceDaysElapsed = if (isInGracePeriod && expiryDate != null) {
		maxOf(0, ceil((System.currentTimeMillis() - expiryDate.toEpochMilli()).toDouble() / DAY_IN_MILLIS).toInt())
	} else {
		0
	}
	val daysRemaining = remember(expiryDate) {
		if (expiryDate == null) {
			0
		} else {
			maxOf(0, ceil((expiryDate.toEpochMilli() - System.currentTimeMillis()).toDouble() / DAY_IN_MILLIS).toInt())
		}
	}
	val lastDurationMonths = subscription?.lastDurationMonths
	val totalPlanDays = lastDurationMonths?.let { PLAN_DURATION_DAYS[it] } ?: 0
	val progressPercent = if (isInGracePeriod) {
		if (graceDaysTotal <= 0) {
			0
		} else {
			((graceDaysRemaining.toDouble() / graceDaysTotal.toDouble()) * 100.0).coerceIn(0.0, 100.0).roundToInt()
		}
	} else if (totalPlanDays <= 0) {
		0
	} else {
		((daysRemaining.toDouble() / totalPlanDays.toDouble()) * 100.0).coerceIn(0.0, 100.0).roundToInt()
	}
	val progressColor = when {
		isInGracePeriod -> Tokens.Color.colorOrange400
		daysRemaining > 30 -> Tokens.Color.colorGreen400
		daysRemaining >= 7 -> Tokens.Color.colorOrange400
		else -> Tokens.Color.colorRed400
	}
	val currentPlanTitleRes = when (lastDurationMonths) {
		1 -> R.string.subscription_plan_starter
		3 -> R.string.subscription_plan_standard
		6 -> R.string.subscription_plan_pro
		12 -> R.string.subscription_plan_annual
		else -> R.string.subscription_plan_unknown
	}
	val statusText = subscription?.status?.takeUnless { it.isBlank() } ?: when {
		isInGracePeriod -> stringResource(R.string.subscription_status_grace)
		expiryDate != null && !expiryDate.isAfter(Instant.now()) -> stringResource(R.string.subscription_status_expired)
		else -> stringResource(R.string.subscription_status_active)
	}

	ProvideTextStyle(JellyfinTheme.typography.default.copy(color = JellyfinTheme.colorScheme.listHeadline)) {
		LazyColumn(
			state = listState,
			modifier = Modifier
				.fillMaxSize()
				.padding(horizontal = 42.dp, vertical = 28.dp),
			verticalArrangement = Arrangement.spacedBy(16.dp),
		) {
			item {
				SubscriptionHeader(
					userName = uiState.userName,
				)
			}

		item {
			SubscriptionStatusCard(
				currentPlanTitle = stringResource(currentPlanTitleRes),
				statusText = statusText,
				validUntil = formatDate(expiryDate),
				daysRemaining = daysRemaining,
				isInGracePeriod = isInGracePeriod,
				graceDaysElapsed = graceDaysElapsed,
				graceDaysRemaining = graceDaysRemaining,
				graceDaysTotal = graceDaysTotal,
				progressPercent = progressPercent,
				progressColor = progressColor,
				autoRenewEnabled = uiState.autoRenewEnabled,
				onAutoRenewToggle = onAutoRenewToggle,
				showRenewNow = isInGracePeriod || daysRemaining < 7,
				onRenewNow = {
					scope.launch {
						listState.animateScrollToItem(RENEW_SCROLL_TARGET_INDEX)
						firstPlanFocusRequester.requestFocus()
					}
				},
				isLoading = uiState.isLoading,
				errorMessage = uiState.statusErrorMessage,
			)
		}

		item {
			Column(
				verticalArrangement = Arrangement.spacedBy(12.dp),
			) {
				Text(
					text = stringResource(R.string.subscription_plans_title),
					fontSize = 28.sp,
					fontWeight = FontWeight.Bold,
				)

				PLANS.forEachIndexed { index, plan ->
					val actualPrice = uiState.pricingConfig.priceForMonths(plan.months)
					val originalPrice = uiState.pricingConfig.basePricePerMonth * plan.months
					val savingsAmount = originalPrice - actualPrice
					val hasSavings = savingsAmount > 0
					val savingsPercent = if (hasSavings && originalPrice > 0) {
						((savingsAmount / originalPrice) * 100).toInt()
					} else {
						0
					}
					val isLastPlan = lastDurationMonths == plan.months

					SubscriptionPlanCard(
						plan = plan,
						actualPrice = actualPrice,
						originalPrice = originalPrice,
						savingsAmount = savingsAmount,
						savingsPercent = savingsPercent,
						hasSavings = hasSavings,
						isLastPlan = isLastPlan,
						formatPrice = formatPrice,
						modifier = if (index == 0) {
							Modifier.focusRequester(firstPlanFocusRequester)
						} else {
							Modifier
						},
					)
				}
			}
		}

			item {
				SubscriptionRedeemCard(
					accessKey = uiState.accessKey,
					onAccessKeyChange = onAccessKeyChange,
					onRedeemAccessKey = onRedeemAccessKey,
					isRedeeming = uiState.isRedeeming,
					redeemErrorMessage = uiState.redeemErrorMessage,
					redeemSuccessMessage = uiState.redeemSuccessMessage,
				)
			}
		}
	}
}

@Composable
private fun SubscriptionHeader(
	userName: String,
) {
	Column(
		verticalArrangement = Arrangement.spacedBy(8.dp),
	) {
		Text(
			text = stringResource(R.string.subscription_management_title),
			fontSize = 36.sp,
			fontWeight = FontWeight.Bold,
		)
		Text(
			text = stringResource(R.string.subscription_management_subtitle),
			fontSize = 18.sp,
			color = JellyfinTheme.colorScheme.listCaption,
		)
		if (userName.isNotBlank()) {
			Text(
				text = stringResource(R.string.subscription_signed_in_as, userName),
				fontSize = 16.sp,
				color = JellyfinTheme.colorScheme.listCaption,
			)
		}
	}
}

@Composable
private fun SubscriptionStatusCard(
	currentPlanTitle: String,
	statusText: String,
	validUntil: String,
	daysRemaining: Int,
	isInGracePeriod: Boolean,
	graceDaysElapsed: Int,
	graceDaysRemaining: Int,
	graceDaysTotal: Int,
	progressPercent: Int,
	progressColor: Color,
	autoRenewEnabled: Boolean,
	onAutoRenewToggle: (Boolean) -> Unit,
	showRenewNow: Boolean,
	onRenewNow: () -> Unit,
	isLoading: Boolean,
	errorMessage: String?,
) {
	SubscriptionCardContainer {
		Column(
			verticalArrangement = Arrangement.spacedBy(12.dp),
		) {
			Row(
				modifier = Modifier.fillMaxWidth(),
				horizontalArrangement = Arrangement.SpaceBetween,
				verticalAlignment = Alignment.CenterVertically,
			) {
				Column(
					verticalArrangement = Arrangement.spacedBy(8.dp),
				) {
					Text(
						text = stringResource(R.string.subscription_current_plan_title),
						fontSize = 24.sp,
						fontWeight = FontWeight.Bold,
					)
					PlanBadge(
						label = currentPlanTitle,
						brush = Brush.horizontalGradient(
							colors = listOf(
								JellyfinTheme.colorScheme.badge.copy(alpha = 0.66f),
								JellyfinTheme.colorScheme.listButtonFocused.copy(alpha = 0.92f),
							),
						),
					)
				}

				AutoRenewToggle(
					enabled = autoRenewEnabled,
					onToggle = onAutoRenewToggle,
				)
			}

			if (isLoading) {
				Text(
					text = stringResource(R.string.subscription_loading),
					fontSize = 15.sp,
					color = JellyfinTheme.colorScheme.listCaption,
				)
			}

			if (!errorMessage.isNullOrBlank()) {
				Text(
					text = errorMessage,
					fontSize = 15.sp,
					color = Tokens.Color.colorOrange300,
				)
			}

			if (isInGracePeriod) {
				Column(
					modifier = Modifier
						.fillMaxWidth()
						.clip(JellyfinTheme.shapes.large)
						.background(Tokens.Color.colorOrange900.copy(alpha = 0.32f))
						.border(
							width = 1.dp,
							color = Tokens.Color.colorOrange400.copy(alpha = 0.6f),
							shape = JellyfinTheme.shapes.large,
						)
						.padding(horizontal = 12.dp, vertical = 10.dp),
					verticalArrangement = Arrangement.spacedBy(6.dp),
				) {
					Text(
						text = stringResource(R.string.subscription_grace_banner_title),
						fontSize = 16.sp,
						fontWeight = FontWeight.Bold,
						color = Tokens.Color.colorOrange200,
					)
					Text(
						text = stringResource(
							R.string.subscription_grace_banner_body,
							graceDaysElapsed,
							graceDaysTotal,
							graceDaysRemaining,
						),
						fontSize = 14.sp,
						color = JellyfinTheme.colorScheme.listHeadline.copy(alpha = 0.94f),
					)
				}
			}

			Text(
				text = stringResource(R.string.subscription_status_format, statusText),
				fontSize = 17.sp,
			)
			Text(
				text = stringResource(R.string.subscription_valid_until_format, validUntil),
				fontSize = 17.sp,
			)
			Text(
				text = if (isInGracePeriod) {
					pluralStringResource(
						R.plurals.subscription_grace_days_remaining,
						graceDaysRemaining,
						graceDaysRemaining,
					)
				} else {
					pluralStringResource(R.plurals.subscription_days_remaining, daysRemaining, daysRemaining)
				},
				fontSize = 20.sp,
				fontWeight = FontWeight.Bold,
				color = progressColor,
			)

			Box(
				modifier = Modifier
					.fillMaxWidth()
					.height(12.dp)
					.clip(RoundedCornerShape(999.dp))
					.background(JellyfinTheme.colorScheme.listButtonFocused.copy(alpha = 0.4f))
			) {
				Box(
					modifier = Modifier
						.fillMaxWidth((progressPercent / 100f).coerceIn(0f, 1f))
						.fillMaxHeight()
						.clip(RoundedCornerShape(999.dp))
						.background(progressColor)
				)
			}

			Text(
				text = stringResource(R.string.subscription_cycle_remaining_format, progressPercent),
				fontSize = 14.sp,
				color = JellyfinTheme.colorScheme.listCaption,
			)

			if (showRenewNow) {
				Button(
					onClick = onRenewNow,
					modifier = Modifier.heightIn(min = 48.dp),
				) {
					Text(
						text = stringResource(R.string.subscription_renew_now),
						fontWeight = FontWeight.Bold,
					)
				}
			}
		}
	}
}

@Composable
private fun AutoRenewToggle(
	enabled: Boolean,
	onToggle: (Boolean) -> Unit,
) {
	@OptIn(ExperimentalFoundationApi::class)
	val interactionSource = remember { MutableInteractionSource() }
	val focused by interactionSource.collectIsFocusedAsState()
	val pressed by interactionSource.collectIsPressedAsState()
	val borderColor by animateColorAsState(
		targetValue = if (focused || pressed) {
			JellyfinTheme.colorScheme.badge
		} else {
			JellyfinTheme.colorScheme.listCaption.copy(alpha = 0.4f)
		},
		animationSpec = tween(200),
		label = "autoRenewBorder",
	)
	val backgroundColor by animateColorAsState(
		targetValue = if (pressed) {
			JellyfinTheme.colorScheme.listButtonFocused.copy(alpha = 0.34f)
		} else {
			Color.Transparent
		},
		animationSpec = tween(150),
		label = "autoRenewBackground",
	)
	val scale by animateFloatAsState(
		targetValue = when {
			focused -> 1.03f
			pressed -> 1.015f
			else -> 1f
		},
		animationSpec = tween(180),
		label = "autoRenewScale",
	)

	Column(
		modifier = Modifier
			.graphicsLayer {
				scaleX = scale
				scaleY = scale
			}
			.clip(JellyfinTheme.shapes.large)
			.background(backgroundColor)
			.border(2.dp, borderColor, JellyfinTheme.shapes.large)
			.combinedClickable(
				interactionSource = interactionSource,
				onClick = { onToggle(!enabled) },
			)
			.padding(horizontal = 12.dp, vertical = 10.dp),
		verticalArrangement = Arrangement.spacedBy(6.dp),
		horizontalAlignment = Alignment.CenterHorizontally,
	) {
		Text(
			text = if (enabled) stringResource(R.string.subscription_auto_renew_on) else stringResource(R.string.subscription_auto_renew_off),
			fontSize = 14.sp,
		)
		Checkbox(
			checked = enabled,
			modifier = Modifier
				.width(24.dp)
				.height(24.dp),
		)
	}
}

@Composable
private fun SubscriptionCardContainer(
	content: @Composable ColumnScope.() -> Unit,
) {
	Column(
		modifier = Modifier
			.fillMaxWidth()
			.border(1.dp, JellyfinTheme.colorScheme.listCaption.copy(alpha = 0.3f), JellyfinTheme.shapes.large)
			.clip(JellyfinTheme.shapes.large)
			.background(JellyfinTheme.colorScheme.surface.copy(alpha = 0.84f))
			.padding(20.dp),
		content = content,
	)
}

@OptIn(ExperimentalFoundationApi::class)
@Composable
private fun SubscriptionPlanCard(
	plan: PlanUiState,
	actualPrice: Double,
	originalPrice: Double,
	savingsAmount: Double,
	savingsPercent: Int,
	hasSavings: Boolean,
	isLastPlan: Boolean,
	formatPrice: (Double) -> String,
	modifier: Modifier = Modifier,
) {
	val interactionSource = remember { MutableInteractionSource() }
	val focused by interactionSource.collectIsFocusedAsState()
	val pressed by interactionSource.collectIsPressedAsState()
	val scale by animateFloatAsState(
		targetValue = when {
			focused -> 1.03f
			pressed -> 1.015f
			else -> 1f
		},
		animationSpec = tween(durationMillis = 220),
		label = "planScale",
	)
	val borderColor by animateColorAsState(
		targetValue = when {
			isLastPlan -> Tokens.Color.colorGreen400
			focused || pressed -> JellyfinTheme.colorScheme.badge
			else -> JellyfinTheme.colorScheme.listCaption.copy(alpha = 0.35f)
		},
		animationSpec = tween(220),
		label = "planBorder",
	)
	val backgroundColor by animateColorAsState(
		targetValue = if (focused || pressed) {
			JellyfinTheme.colorScheme.listButtonFocused.copy(alpha = 0.88f)
		} else {
			JellyfinTheme.colorScheme.surface.copy(alpha = 0.8f)
		},
		animationSpec = tween(220),
		label = "planBackground",
	)

	Column(
		modifier = modifier
			.fillMaxWidth()
			.heightIn(min = 180.dp)
			.graphicsLayer {
				scaleX = scale
				scaleY = scale
			}
			.shadow(
				elevation = if (focused || pressed) 18.dp else 4.dp,
				shape = JellyfinTheme.shapes.large,
				ambientColor = borderColor.copy(alpha = if (focused || pressed) 0.45f else 0.15f),
				spotColor = borderColor.copy(alpha = if (focused || pressed) 0.35f else 0.1f),
			)
			.border(2.dp, borderColor, JellyfinTheme.shapes.large)
			.clip(JellyfinTheme.shapes.large)
			.background(backgroundColor)
			.combinedClickable(
				interactionSource = interactionSource,
				onClick = {
					// Intentionally empty until payment flow is implemented.
				},
			)
			.padding(16.dp),
		verticalArrangement = Arrangement.spacedBy(8.dp),
	) {
		Row(
			modifier = Modifier.fillMaxWidth(),
			horizontalArrangement = Arrangement.SpaceBetween,
			verticalAlignment = Alignment.CenterVertically,
		) {
			Text(
				text = stringResource(plan.titleRes),
				fontSize = 26.sp,
				fontWeight = FontWeight.Bold,
			)

			if (plan.isPopular) {
				PlanBadge(
					label = stringResource(R.string.subscription_popular_badge),
					brush = Brush.horizontalGradient(
						colors = listOf(
							Tokens.Color.colorOrange300,
							Tokens.Color.colorOrange500,
						),
					),
				)
			}
		}

		Text(
			text = stringResource(plan.descriptionRes),
			fontSize = 16.sp,
			color = JellyfinTheme.colorScheme.listCaption,
		)

		if (hasSavings) {
			Text(
				text = stringResource(R.string.subscription_price_format, formatPrice(originalPrice)),
				fontSize = 17.sp,
				color = JellyfinTheme.colorScheme.listCaption,
				textDecoration = TextDecoration.LineThrough,
			)
		}

		Text(
			text = stringResource(R.string.subscription_price_format, formatPrice(actualPrice)),
			fontSize = 34.sp,
			fontWeight = FontWeight.Bold,
		)

		if (hasSavings) {
			Text(
				text = stringResource(R.string.subscription_savings_format, formatPrice(savingsAmount), savingsPercent),
				fontSize = 14.sp,
				fontWeight = FontWeight.SemiBold,
				color = Tokens.Color.colorGreen200,
			)
		}

		Text(
			text = pluralStringResource(R.plurals.subscription_month_duration, plan.months, plan.months),
			fontSize = 15.sp,
			color = JellyfinTheme.colorScheme.listCaption,
		)

		if (isLastPlan) {
			PlanBadge(
				label = stringResource(R.string.subscription_last_plan_badge),
				brush = Brush.horizontalGradient(
					colors = listOf(
						Tokens.Color.colorGreen500.copy(alpha = 0.6f),
						Tokens.Color.colorGreen700.copy(alpha = 0.62f),
					),
				),
			)
		}
	}
}

@Composable
private fun PlanBadge(
	label: String,
	brush: Brush,
) {
	Text(
		text = label,
		modifier = Modifier
			.clip(RoundedCornerShape(999.dp))
			.background(brush)
			.padding(horizontal = 12.dp, vertical = 6.dp),
		fontSize = 13.sp,
		fontWeight = FontWeight.Bold,
		color = Color.White,
	)
}

@Composable
private fun SubscriptionRedeemCard(
	accessKey: String,
	onAccessKeyChange: (String) -> Unit,
	onRedeemAccessKey: () -> Unit,
	isRedeeming: Boolean,
	redeemErrorMessage: String?,
	redeemSuccessMessage: String?,
) {
	SubscriptionCardContainer {
		Column(
			verticalArrangement = Arrangement.spacedBy(12.dp),
		) {
			Text(
				text = stringResource(R.string.subscription_redeem_panel_title),
				fontSize = 24.sp,
				fontWeight = FontWeight.Bold,
			)

			SubscriptionAccessKeyInput(
				value = accessKey,
				onValueChange = onAccessKeyChange,
			)

			Button(
				onClick = onRedeemAccessKey,
				enabled = !isRedeeming && accessKey.isNotBlank(),
				modifier = Modifier.heightIn(min = 48.dp),
			) {
				Text(
					text = if (isRedeeming) {
						stringResource(R.string.subscription_redeeming_action)
					} else {
						stringResource(R.string.subscription_redeem_action)
					},
					fontWeight = FontWeight.Bold,
				)
			}

			if (!redeemSuccessMessage.isNullOrBlank()) {
				Text(
					text = redeemSuccessMessage,
					fontSize = 15.sp,
					color = Tokens.Color.colorGreen200,
				)
			}

			if (!redeemErrorMessage.isNullOrBlank()) {
				Text(
					text = redeemErrorMessage,
					fontSize = 15.sp,
					color = Tokens.Color.colorRed200,
				)
			}
		}
	}
}

@Composable
private fun SubscriptionAccessKeyInput(
	value: String,
	onValueChange: (String) -> Unit,
) {
	val interactionSource = remember { MutableInteractionSource() }
	val focused by interactionSource.collectIsFocusedAsState()
	val borderColor by animateColorAsState(
		targetValue = if (focused) JellyfinTheme.colorScheme.badge else JellyfinTheme.colorScheme.listCaption.copy(alpha = 0.5f),
		animationSpec = tween(180),
		label = "redeemInputBorder",
	)

	BasicTextField(
		value = value,
		singleLine = true,
		interactionSource = interactionSource,
		onValueChange = onValueChange,
		keyboardActions = KeyboardActions.Default,
		keyboardOptions = KeyboardOptions.Default.copy(
			keyboardType = KeyboardType.Text,
			imeAction = ImeAction.Done,
			autoCorrectEnabled = false,
			showKeyboardOnFocus = true,
		),
		textStyle = TextStyle(
			color = JellyfinTheme.colorScheme.listHeadline,
			fontSize = 18.sp,
		),
		cursorBrush = SolidColor(JellyfinTheme.colorScheme.badge),
		decorationBox = { innerTextField ->
			Column(
				modifier = Modifier
					.fillMaxWidth()
					.heightIn(min = 52.dp)
					.clip(JellyfinTheme.shapes.large)
					.border(2.dp, borderColor, JellyfinTheme.shapes.large)
					.background(JellyfinTheme.colorScheme.input.copy(alpha = 0.55f))
					.padding(horizontal = 14.dp, vertical = 10.dp),
				verticalArrangement = Arrangement.spacedBy(4.dp),
			) {
				Text(
					text = stringResource(R.string.subscription_access_key_label),
					fontSize = 12.sp,
					color = JellyfinTheme.colorScheme.listCaption,
				)
				Box(
					contentAlignment = Alignment.CenterStart,
				) {
					if (value.isBlank()) {
						Text(
							text = stringResource(R.string.subscription_access_key_placeholder),
							fontSize = 18.sp,
							color = JellyfinTheme.colorScheme.listCaption.copy(alpha = 0.8f),
						)
					}
					innerTextField()
				}
			}
		},
	)
}

private fun PricingConfig.priceForMonths(months: Int): Double {
	return when (months) {
		1 -> oneMonthPrice
		3 -> threeMonthPrice
		6 -> sixMonthPrice
		12 -> twelveMonthPrice
		else -> oneMonthPrice
	}
}

private fun fetchCurrentSubscription(baseUrl: String, accessToken: String): CurrentSubscription {
	val body = fetchEndpointBody(baseUrl, ENDPOINT_CURRENT_SUBSCRIPTION, accessToken)
	return try {
		val payload = JSONObject(body)
		val status = payload.optStringAny("Status", "status").orEmpty()
		val isInGracePeriod = payload.optBooleanAny("IsInGracePeriod", "isInGracePeriod")
			?: status.equals("Grace", ignoreCase = true)
		CurrentSubscription(
			expiryDate = parseInstant(payload.optStringAny("ExpiryDate", "expiryDate")),
			status = status,
			isInGracePeriod = isInGracePeriod,
			graceDaysRemaining = (
				payload.optIntAny("GraceDaysRemaining", "graceDaysRemaining") ?: 0
				).coerceAtLeast(0),
			lastDurationMonths = payload.optIntAny("LastDurationMonths", "lastDurationMonths")
				?.takeIf { it in SUPPORTED_MONTHS },
		)
	} catch (error: JSONException) {
		throw IOException("Unable to parse current subscription metadata", error)
	}
}

private fun fetchSubscriptionPricing(baseUrl: String, accessToken: String): PricingConfig {
	val body = fetchEndpointBody(baseUrl, ENDPOINT_PRICING, accessToken)
	return try {
		val payload = JSONObject(body)
		val gracePeriodDays = (
			payload.optIntAny("GracePeriodDays", "gracePeriodDays") ?: 3
			).coerceAtLeast(0)
		val basePrice = parsePositiveDouble(
			payload.optAny("BasePricePerMonth", "basePricePerMonth"),
			100.0,
		)

		var oneMonthPrice = parsePositiveDouble(
			payload.optAny("OneMonthPrice", "oneMonthPrice", "Price1Month", "price1Month"),
			100.0,
		)
		var threeMonthPrice = parsePositiveDouble(
			payload.optAny("ThreeMonthPrice", "threeMonthPrice", "Price3Month", "price3Month"),
			250.0,
		)
		var sixMonthPrice = parsePositiveDouble(
			payload.optAny("SixMonthPrice", "sixMonthPrice", "Price6Month", "price6Month"),
			450.0,
		)
		var twelveMonthPrice = parsePositiveDouble(
			payload.optAny("TwelveMonthPrice", "twelveMonthPrice", "Price12Month", "price12Month"),
			850.0,
		)

		val plans = payload.optJSONArray("Plans") ?: payload.optJSONArray("plans")
		if (plans != null) {
			val mappedPlans = parsePlanPricing(plans)
			oneMonthPrice = mappedPlans[1] ?: oneMonthPrice
			threeMonthPrice = mappedPlans[3] ?: threeMonthPrice
			sixMonthPrice = mappedPlans[6] ?: sixMonthPrice
			twelveMonthPrice = mappedPlans[12] ?: twelveMonthPrice
		}

		PricingConfig(
			gracePeriodDays = gracePeriodDays,
			basePricePerMonth = basePrice,
			oneMonthPrice = oneMonthPrice,
			threeMonthPrice = threeMonthPrice,
			sixMonthPrice = sixMonthPrice,
			twelveMonthPrice = twelveMonthPrice,
		)
	} catch (error: JSONException) {
		throw IOException("Unable to parse subscription pricing metadata", error)
	}
}

private fun parsePlanPricing(plans: JSONArray): Map<Int, Double> {
	val mapped = mutableMapOf<Int, Double>()
	repeat(plans.length()) { index ->
		val item = plans.optJSONObject(index) ?: return@repeat
		val months = item.optIntAny("Months", "months", "DurationMonths", "durationMonths")
			?: return@repeat
		if (months !in SUPPORTED_MONTHS) return@repeat

		val price = parsePositiveDouble(
			item.optAny("Price", "price"),
			Double.NaN,
		)
		if (price.isFinite() && price > 0) {
			mapped[months] = price
		}
	}
	return mapped
}

private fun redeemAccessKey(baseUrl: String, accessToken: String, key: String) {
	val payload = JSONObject().put("Key", key).toString()
	val response = executeRequest(
		baseUrl = baseUrl,
		path = ENDPOINT_REDEEM,
		accessToken = accessToken,
		method = "POST",
		requestBody = payload,
	)
	if (response.statusCode !in 200..299) {
		throw IOException(
			extractServerErrorMessage(response.body)
				?: "Redeem failed with HTTP ${response.statusCode}"
		)
	}
}

private fun fetchEndpointBody(baseUrl: String, path: String, accessToken: String): String {
	val response = executeRequest(
		baseUrl = baseUrl,
		path = path,
		accessToken = accessToken,
		method = "GET",
	)
	if (response.statusCode !in 200..299) {
		throw HttpStatusException(
			statusCode = response.statusCode,
			message = extractServerErrorMessage(response.body)
				?: "Request failed for $path with HTTP ${response.statusCode}"
		)
	}

	return response.body.orEmpty()
}

private fun executeRequest(
	baseUrl: String,
	path: String,
	accessToken: String,
	method: String,
	requestBody: String? = null,
): HttpResponse {
	val normalizedBaseUrl = baseUrl.trimEnd('/')
	val normalizedPath = path.trimStart('/')
	val endpoint = "$normalizedBaseUrl/$normalizedPath?api_key=${
		URLEncoder.encode(accessToken, StandardCharsets.UTF_8.name())
	}"

	val request = URL(endpoint).openConnection() as HttpURLConnection
	request.requestMethod = method
	request.connectTimeout = 10000
	request.readTimeout = 10000
	request.setRequestProperty("Accept", "application/json")
	if (requestBody != null) {
		request.doOutput = true
		request.setRequestProperty("Content-Type", "application/json")
		request.outputStream.bufferedWriter(StandardCharsets.UTF_8).use { writer ->
			writer.write(requestBody)
		}
	}

	return try {
		val statusCode = request.responseCode
		val bodyStream = if (statusCode in 200..299) request.inputStream else request.errorStream
		val body = bodyStream?.bufferedReader()?.use { it.readText() }
		HttpResponse(
			statusCode = statusCode,
			body = body,
		)
	} finally {
		request.disconnect()
	}
}

private fun extractServerErrorMessage(body: String?): String? {
	if (body.isNullOrBlank()) {
		return null
	}

	val trimmedBody = body.trim()
	val parsedMessage = runCatching {
		when (val payload = JSONTokener(trimmedBody).nextValue()) {
			is JSONObject -> payload.optStringAny("message", "Message", "error", "Error")
			is String -> payload
			else -> null
		}
	}.getOrNull()

	return parsedMessage?.takeUnless { it.isBlank() } ?: trimmedBody.take(220)
}

private fun parsePositiveDouble(value: Any?, fallback: Double): Double {
	val parsedValue = when (value) {
		is Number -> value.toDouble()
		is String -> value.toDoubleOrNull()
		else -> null
	}

	return if (parsedValue != null && parsedValue.isFinite() && parsedValue > 0.0) parsedValue else fallback
}

private fun JSONObject.optAny(vararg keys: String): Any? {
	for (key in keys) {
		if (has(key) && !isNull(key)) {
			return opt(key)
		}
	}
	return null
}

private fun JSONObject.optStringAny(vararg keys: String): String? {
	val value = optAny(*keys) ?: return null
	return value.toString().takeUnless { it.isBlank() || it == "null" }
}

private fun JSONObject.optIntAny(vararg keys: String): Int? {
	val value = optAny(*keys) ?: return null
	return when (value) {
		is Number -> value.toInt()
		is String -> value.toIntOrNull()
		else -> null
	}
}

private fun JSONObject.optBooleanAny(vararg keys: String): Boolean? {
	val value = optAny(*keys) ?: return null
	return when (value) {
		is Boolean -> value
		is Number -> value.toInt() != 0
		is String -> value.equals("true", ignoreCase = true)
		else -> null
	}
}

private fun parseInstant(value: String?): Instant? {
	if (value.isNullOrBlank()) {
		return null
	}

	return runCatching { Instant.parse(value) }.getOrElse {
		runCatching { OffsetDateTime.parse(value).toInstant() }.getOrElse {
			runCatching { Instant.parse("${value}Z") }.getOrElse { error ->
				if (error is DateTimeParseException) {
					Timber.w(error, "Unable to parse instant value: %s", value)
				}
				null
			}
		}
	}
}
