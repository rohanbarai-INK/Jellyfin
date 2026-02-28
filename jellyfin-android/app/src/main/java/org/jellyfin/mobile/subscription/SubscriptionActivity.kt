package org.jellyfin.mobile.subscription

import android.animation.ValueAnimator
import android.graphics.Color
import android.graphics.ColorFilter
import android.graphics.LinearGradient
import android.graphics.Matrix
import android.graphics.Paint
import android.graphics.PixelFormat
import android.graphics.Rect
import android.graphics.RectF
import android.graphics.Shader
import android.graphics.drawable.Drawable
import android.os.Bundle
import android.view.MotionEvent
import android.view.View
import android.view.animation.LinearInterpolator
import android.widget.TextView
import androidx.appcompat.app.AppCompatActivity
import androidx.core.content.ContextCompat
import androidx.core.widget.doAfterTextChanged
import androidx.lifecycle.lifecycleScope
import com.google.android.material.card.MaterialCardView
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import org.jellyfin.mobile.R
import org.jellyfin.mobile.app.AppPreferences
import org.jellyfin.mobile.databinding.ActivitySubscriptionBinding
import org.jellyfin.mobile.utils.applyWindowInsetsAsMargins
import org.jellyfin.sdk.api.client.ApiClient
import org.json.JSONArray
import org.json.JSONException
import org.json.JSONObject
import org.koin.android.ext.android.inject
import timber.log.Timber
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
import kotlin.math.ceil
import kotlin.math.roundToInt

class SubscriptionActivity : AppCompatActivity() {
    companion object {
        const val EXTRA_SERVER_URL = "org.jellyfin.mobile.intent.extra.SERVER_URL"

        private const val DAY_IN_MILLIS = 24L * 60L * 60L * 1000L
        private const val BORDER_ANIMATION_DURATION_MS = 3000L
        private const val TEXT_ANIMATION_DURATION_MS = 2800L
        private val PLAN_DAYS = mapOf(
            1 to 30,
            3 to 90,
            6 to 180,
            12 to 365,
        )
        private val REDEEM_RAINBOW_COLORS = intArrayOf(
            Color.WHITE,
            Color.WHITE,
            Color.parseColor("#69F6FF"),
            Color.parseColor("#6688FF"),
            Color.parseColor("#C07AFF"),
            Color.parseColor("#FF7CC6"),
            Color.parseColor("#FF6E60"),
            Color.parseColor("#FFD762"),
            Color.parseColor("#ACFF80"),
            Color.WHITE,
            Color.WHITE,
        )
    }

    private data class CurrentSubscription(
        val expiryDate: Instant?,
        val status: String,
        val isInGracePeriod: Boolean,
        val graceDaysRemaining: Int,
        val lastDurationMonths: Int?,
    )

    private data class PricingConfig(
        val gracePeriodDays: Int,
        val basePricePerMonth: Double,
        val oneMonthPrice: Double,
        val threeMonthPrice: Double,
        val sixMonthPrice: Double,
        val twelveMonthPrice: Double,
    )

    private data class PlanViews(
        val card: MaterialCardView,
        val originalPrice: TextView,
        val price: TextView,
        val savings: TextView,
        val duration: TextView,
        val lastPlanBadge: TextView,
        val months: Int,
        val isPopular: Boolean = false,
    )

    private data class EndpointResult(
        val statusCode: Int,
        val responseBody: String?,
    ) {
        val isSuccessful: Boolean get() = statusCode in 200..299
    }

    private data class RedeemResult(
        val success: Boolean,
        val errorMessage: String? = null,
    )

    private lateinit var binding: ActivitySubscriptionBinding
    private val apiClient: ApiClient by inject()
    private val appPreferences: AppPreferences by inject()
    private var redeemBorderDrawable: AnimatedRainbowBorderDrawable? = null
    private var redeemBorderAnimator: ValueAnimator? = null
    private var redeemTextAnimator: ValueAnimator? = null
    private var redeemTextShader: LinearGradient? = null
    private val redeemTextShaderMatrix = Matrix()
    private val numberFormatter = DecimalFormat("#.##").apply {
        roundingMode = RoundingMode.HALF_UP
    }
    private val dateFormatter = DateTimeFormatter.ofPattern("dd MMM yyyy", Locale.getDefault())
        .withZone(ZoneId.systemDefault())

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        binding = ActivitySubscriptionBinding.inflate(layoutInflater)
        setContentView(binding.root)

        binding.root.applyWindowInsetsAsMargins()
        binding.toolbar.setTitle(R.string.subscription_title)
        setSupportActionBar(binding.toolbar)
        supportActionBar?.setDisplayHomeAsUpEnabled(true)

        binding.autoRenewSwitch.isChecked = appPreferences.subscriptionAutoRenewVisual
        binding.autoRenewSwitch.text = if (appPreferences.subscriptionAutoRenewVisual) {
            getString(R.string.subscription_auto_renew_on)
        } else {
            getString(R.string.subscription_auto_renew_off)
        }
        binding.autoRenewSwitch.setOnCheckedChangeListener { _, isChecked ->
            appPreferences.subscriptionAutoRenewVisual = isChecked
            binding.autoRenewSwitch.text = if (isChecked) {
                getString(R.string.subscription_auto_renew_on)
            } else {
                getString(R.string.subscription_auto_renew_off)
            }
        }

        binding.renewNowButton.setOnClickListener {
            binding.subscriptionScrollView.smoothScrollTo(0, binding.plansContainer.top)
        }
        binding.redeemKeyInput.doAfterTextChanged {
            if (binding.redeemMessageText.visibility == View.VISIBLE) {
                binding.redeemMessageText.visibility = View.GONE
            }
            updateRedeemButtonEnabledState()
        }
        binding.redeemKeyButton.setOnClickListener {
            onRedeemAccessKey()
        }
        setupAnimatedRedeemButton()
        updateRedeemButtonEnabledState()

        lifecycleScope.launch {
            loadSubscriptionData()
        }
    }

    private suspend fun loadSubscriptionData() {
        val serverContext = resolveServerContext()

        if (serverContext == null) {
            Timber.w("Unable to load subscription metadata due to missing server context")
            renderPlans(PricingConfig(3, 100.0, 100.0, 250.0, 450.0, 850.0), null)
            renderCurrentPlanCard(null, 3)
            return
        }
        val (baseUrl, accessToken) = serverContext

        val (currentSubscription, pricingConfig) = withContext(Dispatchers.IO) {
            val current = fetchCurrentSubscription(baseUrl, accessToken)
            val pricing = fetchSubscriptionPricing(baseUrl, accessToken)
            current to pricing
        }

        val resolvedPricing = pricingConfig ?: PricingConfig(3, 100.0, 100.0, 250.0, 450.0, 850.0)
        renderCurrentPlanCard(currentSubscription, resolvedPricing.gracePeriodDays)
        renderPlans(resolvedPricing, currentSubscription?.lastDurationMonths)
    }

    private fun renderCurrentPlanCard(currentSubscription: CurrentSubscription?, gracePeriodDays: Int) {
        val planName = when (currentSubscription?.lastDurationMonths) {
            1 -> getString(R.string.subscription_plan_starter)
            3 -> getString(R.string.subscription_plan_standard)
            6 -> getString(R.string.subscription_plan_pro)
            12 -> getString(R.string.subscription_plan_annual)
            else -> getString(R.string.subscription_plan_unknown)
        }
        binding.currentPlanBadge.text = planName

        val expiryDate = currentSubscription?.expiryDate
        val isInGracePeriod = currentSubscription?.isInGracePeriod == true
        val status = currentSubscription?.status?.takeUnless(String::isBlank)
            ?: when {
                isInGracePeriod -> getString(R.string.subscription_status_grace)
                expiryDate != null && expiryDate.isBefore(Instant.now()) -> getString(R.string.subscription_status_expired)
                else -> getString(R.string.subscription_status_active)
            }
        binding.subscriptionStatusText.text = getString(R.string.subscription_status_format, status)

        val validUntil = expiryDate?.let { dateFormatter.format(it) } ?: getString(R.string.subscription_not_set)
        binding.validUntilText.text = getString(R.string.subscription_valid_until_format, validUntil)

        val daysRemaining = if (expiryDate == null) {
            0
        } else {
            val diffMillis = expiryDate.toEpochMilli() - System.currentTimeMillis()
            maxOf(0, ceil(diffMillis.toDouble() / DAY_IN_MILLIS.toDouble()).toInt())
        }

        val graceDaysRemaining = currentSubscription?.graceDaysRemaining?.coerceAtLeast(0) ?: 0
        val configuredGraceDays = gracePeriodDays.coerceAtLeast(0)
        val graceDaysElapsed = if (expiryDate == null) {
            0
        } else {
            val elapsedMillis = System.currentTimeMillis() - expiryDate.toEpochMilli()
            maxOf(0, ceil(elapsedMillis.toDouble() / DAY_IN_MILLIS.toDouble()).toInt())
        }

        val totalPlanDays = PLAN_DAYS[currentSubscription?.lastDurationMonths] ?: 0
        val progressPercent = if (isInGracePeriod) {
            if (configuredGraceDays > 0) {
                ((graceDaysRemaining.toDouble() / configuredGraceDays.toDouble()) * 100.0).coerceIn(0.0, 100.0).roundToInt()
            } else {
                0
            }
        } else if (totalPlanDays <= 0) {
            0
        } else {
            ((daysRemaining.toDouble() / totalPlanDays.toDouble()) * 100.0).coerceIn(0.0, 100.0).roundToInt()
        }

        val progressColorRes = when {
            isInGracePeriod -> R.color.subscription_progress_orange
            daysRemaining > 30 -> R.color.subscription_progress_green
            daysRemaining >= 7 -> R.color.subscription_progress_orange
            else -> R.color.subscription_progress_red
        }
        val progressColor = ContextCompat.getColor(this, progressColorRes)

        if (isInGracePeriod) {
            binding.daysRemainingText.text = resources.getQuantityString(
                R.plurals.subscription_grace_days_remaining,
                graceDaysRemaining,
                graceDaysRemaining,
            )
        } else {
            binding.daysRemainingText.text = resources.getQuantityString(
                R.plurals.subscription_days_remaining,
                daysRemaining,
                daysRemaining,
            )
        }
        binding.daysRemainingText.setTextColor(progressColor)
        binding.planProgressBar.progressTintList = android.content.res.ColorStateList.valueOf(progressColor)
        binding.planProgressBar.progress = progressPercent
        binding.progressPercentText.text = getString(R.string.subscription_cycle_remaining_format, progressPercent)
        binding.renewNowButton.visibility = if (isInGracePeriod || daysRemaining < 7) View.VISIBLE else View.GONE

        if (isInGracePeriod) {
            binding.graceBannerContainer.visibility = View.VISIBLE
            binding.graceBannerTitle.text = getString(R.string.subscription_grace_banner_title)
            binding.graceBannerBody.text = getString(
                R.string.subscription_grace_banner_body,
                graceDaysElapsed,
                configuredGraceDays,
                graceDaysRemaining,
            )
        } else {
            binding.graceBannerContainer.visibility = View.GONE
        }
    }

    private fun renderPlans(pricingConfig: PricingConfig, lastDurationMonths: Int?) {
        val plans = listOf(
            PlanViews(
                card = binding.cardStarter,
                originalPrice = binding.starterOriginalPrice,
                price = binding.starterPrice,
                savings = binding.starterSavings,
                duration = binding.starterDuration,
                lastPlanBadge = binding.starterLastPlanBadge,
                months = 1,
            ),
            PlanViews(
                card = binding.cardStandard,
                originalPrice = binding.standardOriginalPrice,
                price = binding.standardPrice,
                savings = binding.standardSavings,
                duration = binding.standardDuration,
                lastPlanBadge = binding.standardLastPlanBadge,
                months = 3,
            ),
            PlanViews(
                card = binding.cardPro,
                originalPrice = binding.proOriginalPrice,
                price = binding.proPrice,
                savings = binding.proSavings,
                duration = binding.proDuration,
                lastPlanBadge = binding.proLastPlanBadge,
                months = 6,
                isPopular = true,
            ),
            PlanViews(
                card = binding.cardAnnual,
                originalPrice = binding.annualOriginalPrice,
                price = binding.annualPrice,
                savings = binding.annualSavings,
                duration = binding.annualDuration,
                lastPlanBadge = binding.annualLastPlanBadge,
                months = 12,
            ),
        )

        plans.forEach { plan ->
            val actualPrice = when (plan.months) {
                1 -> pricingConfig.oneMonthPrice
                3 -> pricingConfig.threeMonthPrice
                6 -> pricingConfig.sixMonthPrice
                else -> pricingConfig.twelveMonthPrice
            }
            val originalPrice = pricingConfig.basePricePerMonth * plan.months
            val savingsAmount = originalPrice - actualPrice
            val hasSavings = savingsAmount > 0
            val savingsPercent = if (hasSavings && originalPrice > 0) {
                ((savingsAmount / originalPrice) * 100).toInt()
            } else {
                0
            }
            val isLastPlan = lastDurationMonths != null && lastDurationMonths == plan.months
            bindPlanCard(plan, actualPrice, originalPrice, hasSavings, savingsAmount, savingsPercent, isLastPlan)
        }
    }

    private fun bindPlanCard(
        plan: PlanViews,
        actualPrice: Double,
        originalPrice: Double,
        hasSavings: Boolean,
        savingsAmount: Double,
        savingsPercent: Int,
        isLastPlan: Boolean,
    ) {
        plan.price.text = getString(R.string.subscription_price_format, formatPrice(actualPrice))
        plan.duration.text = resources.getQuantityString(
            R.plurals.subscription_month_duration,
            plan.months,
            plan.months,
        )

        if (hasSavings) {
            plan.originalPrice.visibility = View.VISIBLE
            plan.originalPrice.text = getString(R.string.subscription_price_format, formatPrice(originalPrice))
            plan.originalPrice.paintFlags = plan.originalPrice.paintFlags or Paint.STRIKE_THRU_TEXT_FLAG
            plan.savings.visibility = View.VISIBLE
            plan.savings.text = getString(
                R.string.subscription_savings_format,
                formatPrice(savingsAmount),
                savingsPercent,
            )
        } else {
            plan.originalPrice.visibility = View.GONE
            plan.savings.visibility = View.GONE
        }

        plan.lastPlanBadge.visibility = if (isLastPlan) View.VISIBLE else View.GONE

        val defaultStrokeColor = if (plan.isPopular) {
            ContextCompat.getColor(this, R.color.subscription_popular_stroke)
        } else {
            ContextCompat.getColor(this, R.color.subscription_card_stroke)
        }

        plan.card.strokeWidth = if (isLastPlan) dpToPx(2f) else dpToPx(1f)
        plan.card.strokeColor = if (isLastPlan) {
            ContextCompat.getColor(this, R.color.subscription_last_plan_stroke)
        } else {
            defaultStrokeColor
        }
        plan.card.setOnClickListener {
            // Intentionally empty until payment flow is implemented.
        }
    }

    private fun setupAnimatedRedeemButton() {
        val button = binding.redeemKeyButton
        button.isAllCaps = false
        button.letterSpacing = 0.04f
        button.setTextColor(ContextCompat.getColor(this, R.color.subscription_redeem_enabled_text_fallback))
        button.backgroundTintList = null
        button.elevation = dpToPx(6f).toFloat()

        redeemBorderDrawable = AnimatedRainbowBorderDrawable(
            fillColor = ContextCompat.getColor(this, R.color.subscription_redeem_surface),
            borderWidthPx = dpToPx(1f).toFloat(),
            cornerRadiusPx = dpToPx(10f).toFloat(),
            rainbowColors = REDEEM_RAINBOW_COLORS,
        ).also { button.background = it }

        button.setOnTouchListener { v, event ->
            if (!button.isEnabled) {
                return@setOnTouchListener false
            }

            when (event.actionMasked) {
                MotionEvent.ACTION_DOWN -> {
                    v.animate().scaleX(0.98f).scaleY(0.98f).setDuration(90L).start()
                }
                MotionEvent.ACTION_UP, MotionEvent.ACTION_CANCEL -> {
                    v.animate().scaleX(1f).scaleY(1f).setDuration(130L).start()
                }
            }

            false
        }

        button.post {
            initializeRedeemButtonTextShader()
            updateRedeemButtonVisualState()
        }
    }

    private fun initializeRedeemButtonTextShader() {
        val button = binding.redeemKeyButton
        val textWidth = button.paint.measureText(button.text?.toString().orEmpty()).coerceAtLeast(1f)
        redeemTextShader = LinearGradient(
            -textWidth,
            0f,
            textWidth,
            0f,
            REDEEM_RAINBOW_COLORS,
            null,
            Shader.TileMode.MIRROR,
        )
    }

    private fun updateRedeemButtonTextShader(progress: Float) {
        val button = binding.redeemKeyButton
        val shader = redeemTextShader ?: return
        val width = button.width.toFloat().coerceAtLeast(1f)

        redeemTextShaderMatrix.reset()
        redeemTextShaderMatrix.setTranslate(progress * width * 2f, 0f)
        shader.setLocalMatrix(redeemTextShaderMatrix)
        button.paint.shader = shader
        button.invalidate()
    }

    private fun startRedeemButtonAnimations() {
        stopRedeemButtonAnimations(clearShader = false)

        val button = binding.redeemKeyButton
        val borderDrawable = redeemBorderDrawable ?: return
        if (!button.isEnabled) {
            return
        }

        redeemBorderAnimator = ValueAnimator.ofFloat(0f, 1f).apply {
            duration = BORDER_ANIMATION_DURATION_MS
            interpolator = LinearInterpolator()
            repeatCount = ValueAnimator.INFINITE
            addUpdateListener { animator ->
                borderDrawable.setSweepProgress(animator.animatedValue as Float)
            }
            start()
        }

        redeemTextAnimator = ValueAnimator.ofFloat(0f, 1f).apply {
            duration = TEXT_ANIMATION_DURATION_MS
            interpolator = LinearInterpolator()
            repeatCount = ValueAnimator.INFINITE
            addUpdateListener { animator ->
                updateRedeemButtonTextShader(animator.animatedValue as Float)
            }
            start()
        }
    }

    private fun stopRedeemButtonAnimations(clearShader: Boolean = true) {
        redeemBorderAnimator?.cancel()
        redeemBorderAnimator = null

        redeemTextAnimator?.cancel()
        redeemTextAnimator = null

        if (clearShader) {
            binding.redeemKeyButton.paint.shader = null
            binding.redeemKeyButton.invalidate()
        }
    }

    private fun updateRedeemButtonVisualState() {
        val button = binding.redeemKeyButton
        if (button.isEnabled) {
            button.alpha = 1f
            button.setTextColor(ContextCompat.getColor(this, R.color.subscription_redeem_enabled_text_fallback))
            initializeRedeemButtonTextShader()
            startRedeemButtonAnimations()
        } else {
            stopRedeemButtonAnimations(clearShader = true)
            button.alpha = 0.72f
            button.setTextColor(ContextCompat.getColor(this, R.color.subscription_redeem_disabled_text))
        }
    }

    private fun resolveServerContext(): Pair<String, String>? {
        val baseUrl = (
            intent.getStringExtra(EXTRA_SERVER_URL)?.takeUnless(String::isBlank)
                ?: apiClient.baseUrl
            )?.trimEnd('/')
        val accessToken = apiClient.accessToken?.takeUnless(String::isBlank)

        return if (baseUrl.isNullOrBlank() || accessToken.isNullOrBlank()) {
            null
        } else {
            baseUrl to accessToken
        }
    }

    private fun updateRedeemButtonEnabledState() {
        val hasKey = !binding.redeemKeyInput.text.isNullOrBlank()
        binding.redeemKeyButton.isEnabled = hasKey
        updateRedeemButtonVisualState()
    }

    private fun setRedeemLoadingState(isLoading: Boolean) {
        binding.redeemKeyInput.isEnabled = !isLoading
        binding.redeemKeyButton.text = if (isLoading) {
            getString(R.string.subscription_redeeming_button)
        } else {
            getString(R.string.subscription_redeem_button)
        }

        if (isLoading) {
            binding.redeemKeyButton.isEnabled = false
        } else {
            binding.redeemKeyButton.isEnabled = !binding.redeemKeyInput.text.isNullOrBlank()
        }
        updateRedeemButtonVisualState()
    }

    private fun showRedeemMessage(message: String?, isError: Boolean) {
        if (message.isNullOrBlank()) {
            binding.redeemMessageText.visibility = View.GONE
            return
        }

        val colorRes = if (isError) android.R.color.holo_red_light else android.R.color.holo_green_light
        binding.redeemMessageText.setTextColor(ContextCompat.getColor(this, colorRes))
        binding.redeemMessageText.text = message
        binding.redeemMessageText.visibility = View.VISIBLE
    }

    private fun onRedeemAccessKey() {
        val accessKey = binding.redeemKeyInput.text?.toString()?.trim().orEmpty()
        if (accessKey.isBlank()) {
            showRedeemMessage(getString(R.string.subscription_redeem_key_required), isError = true)
            return
        }

        val serverContext = resolveServerContext()
        if (serverContext == null) {
            showRedeemMessage(getString(R.string.subscription_redeem_server_unavailable), isError = true)
            return
        }
        val (baseUrl, accessToken) = serverContext

        showRedeemMessage(null, isError = false)
        setRedeemLoadingState(true)

        lifecycleScope.launch {
            val redeemResult = withContext(Dispatchers.IO) {
                redeemAccessKey(baseUrl, accessToken, accessKey)
            }

            if (redeemResult.success) {
                binding.redeemKeyInput.setText("")
                showRedeemMessage(getString(R.string.subscription_redeem_success), isError = false)
                loadSubscriptionData()
            } else {
                showRedeemMessage(
                    redeemResult.errorMessage ?: getString(R.string.subscription_redeem_failed),
                    isError = true,
                )
            }

            setRedeemLoadingState(false)
        }
    }

    private fun redeemAccessKey(baseUrl: String, accessToken: String, accessKey: String): RedeemResult {
        val payload = JSONObject().apply {
            put("Key", accessKey)
        }.toString()

        val result = requestEndpoint(baseUrl, "Keys/Redeem", accessToken, method = "POST", body = payload)
        if (result == null) {
            return RedeemResult(
                success = false,
                errorMessage = getString(R.string.subscription_redeem_server_unavailable),
            )
        }

        if (!result.isSuccessful) {
            val errorMessage = getRedeemErrorMessage(result.statusCode, result.responseBody)
            return RedeemResult(success = false, errorMessage = errorMessage)
        }

        return RedeemResult(success = true)
    }

    private fun getRedeemErrorMessage(statusCode: Int, responseBody: String?): String {
        parseServerMessage(responseBody)?.let { return it }

        return when (statusCode) {
            401 -> getString(R.string.subscription_redeem_unauthorized)
            404 -> getString(R.string.subscription_redeem_endpoint_missing)
            else -> getString(R.string.subscription_redeem_failed)
        }
    }

    private fun parseServerMessage(responseBody: String?): String? {
        if (responseBody.isNullOrBlank()) {
            return null
        }

        return try {
            val payload = JSONObject(responseBody)
            for (key in listOf("message", "Message", "error", "Error")) {
                val value = payload.optString(key, "")
                if (value.isNotBlank()) {
                    return value
                }
            }

            null
        } catch (_error: JSONException) {
            responseBody.takeIf { it.isNotBlank() }
        }
    }

    private fun fetchCurrentSubscription(baseUrl: String, accessToken: String): CurrentSubscription? {
        val body = fetchEndpointJson(baseUrl, "Keys/CurrentSubscription", accessToken) ?: return null
        return try {
            val payload = JSONObject(body)
            val statusValue = payload.optString("Status", payload.optString("status", ""))
            val graceDaysRemainingValue = parseNonNegativeInt(
                payload.opt("GraceDaysRemaining") ?: payload.opt("graceDaysRemaining"),
                0,
            )
            val durationValue = parseNonNegativeInt(
                payload.opt("LastDurationMonths") ?: payload.opt("lastDurationMonths"),
                -1,
            )
            val expiryValue = payload.optString("ExpiryDate", payload.optString("expiryDate", ""))
            CurrentSubscription(
                expiryDate = parseInstant(expiryValue.ifBlank { null }),
                status = statusValue,
                isInGracePeriod = payload.optBoolean("IsInGracePeriod", false)
                    || payload.optBoolean("isInGracePeriod", false)
                    || statusValue.equals("Grace", ignoreCase = true),
                graceDaysRemaining = graceDaysRemainingValue,
                lastDurationMonths = durationValue.takeIf { it in PLAN_DAYS.keys },
            )
        } catch (error: JSONException) {
            Timber.w(error, "Unable to parse current subscription payload")
            null
        }
    }

    private fun fetchSubscriptionPricing(baseUrl: String, accessToken: String): PricingConfig? {
        val body = fetchEndpointJson(baseUrl, "System/Configuration/subscription", accessToken) ?: return null
        return try {
            val payload = JSONObject(body)
            val base = parsePositiveDouble(payload.opt("BasePricePerMonth"), 100.0)

            var oneMonth = parsePositiveDouble(payload.opt("OneMonthPrice"), 100.0)
            var threeMonth = parsePositiveDouble(payload.opt("ThreeMonthPrice"), 250.0)
            var sixMonth = parsePositiveDouble(payload.opt("SixMonthPrice"), 450.0)
            var twelveMonth = parsePositiveDouble(payload.opt("TwelveMonthPrice"), 850.0)
            val gracePeriodDays = parseNonNegativeInt(payload.opt("GracePeriodDays"), 3)

            val plans = payload.optJSONArray("Plans")
            if (plans != null) {
                val mappedPlans = parsePlans(pricingPlans = plans)
                oneMonth = mappedPlans[1] ?: oneMonth
                threeMonth = mappedPlans[3] ?: threeMonth
                sixMonth = mappedPlans[6] ?: sixMonth
                twelveMonth = mappedPlans[12] ?: twelveMonth
            }

            PricingConfig(
                gracePeriodDays = gracePeriodDays,
                basePricePerMonth = base,
                oneMonthPrice = oneMonth,
                threeMonthPrice = threeMonth,
                sixMonthPrice = sixMonth,
                twelveMonthPrice = twelveMonth,
            )
        } catch (error: JSONException) {
            Timber.w(error, "Unable to parse subscription pricing payload")
            null
        }
    }

    private fun parsePlans(pricingPlans: JSONArray): Map<Int, Double> {
        val map = mutableMapOf<Int, Double>()
        repeat(pricingPlans.length()) { index ->
            val item = pricingPlans.optJSONObject(index) ?: return@repeat
            val months = item.optInt("Months", -1)
            if (months !in PLAN_DAYS.keys) {
                return@repeat
            }

            val value = parsePositiveDouble(item.opt("Price"), Double.NaN)
            if (value.isFinite() && value > 0) {
                map[months] = value
            }
        }
        return map
    }

    private fun fetchEndpointJson(baseUrl: String, path: String, accessToken: String): String? {
        val result = requestEndpoint(baseUrl, path, accessToken, method = "GET")
        if (result == null) {
            Timber.w("Request failed for %s due to a network error", path)
            return null
        }

        if (!result.isSuccessful) {
            Timber.w("Request failed for %s with code %d. Body: %s", path, result.statusCode, result.responseBody)
            return null
        }

        return result.responseBody
    }

    private fun requestEndpoint(
        baseUrl: String,
        path: String,
        accessToken: String,
        method: String,
        body: String? = null,
    ): EndpointResult? {
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
        if (!body.isNullOrBlank()) {
            request.setRequestProperty("Content-Type", "application/json")
            request.doOutput = true
            request.outputStream.bufferedWriter(StandardCharsets.UTF_8).use { writer ->
                writer.write(body)
            }
        }

        return try {
            val statusCode = request.responseCode
            val stream = if (statusCode in 200..299) request.inputStream else request.errorStream
            val responseBody = stream?.bufferedReader()?.use { it.readText() }
            EndpointResult(statusCode = statusCode, responseBody = responseBody)
        } catch (error: Exception) {
            Timber.w(error, "Failed to request %s", path)
            null
        } finally {
            request.disconnect()
        }
    }

    private fun parsePositiveDouble(value: Any?, fallback: Double): Double {
        val parsed = when (value) {
            is Number -> value.toDouble()
            is String -> value.toDoubleOrNull()
            else -> null
        }

        return if (parsed != null && parsed.isFinite() && parsed > 0.0) parsed else fallback
    }

    private fun parseNonNegativeInt(value: Any?, fallback: Int): Int {
        val parsed = when (value) {
            is Number -> value.toInt()
            is String -> value.toIntOrNull()
            else -> null
        }

        return if (parsed != null && parsed >= 0) parsed else fallback
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

    private fun formatPrice(value: Double): String {
        if (value % 1.0 == 0.0) {
            return value.toInt().toString()
        }

        return numberFormatter.format(value)
    }

    private fun dpToPx(dp: Float): Int = (dp * resources.displayMetrics.density).roundToInt()

    override fun onStart() {
        super.onStart()
        updateRedeemButtonVisualState()
    }

    override fun onStop() {
        stopRedeemButtonAnimations()
        super.onStop()
    }

    override fun onSupportNavigateUp(): Boolean {
        onBackPressedDispatcher.onBackPressed()
        return true
    }
}

private class AnimatedRainbowBorderDrawable(
    private val fillColor: Int,
    private val borderWidthPx: Float,
    private val cornerRadiusPx: Float,
    private val rainbowColors: IntArray,
) : Drawable() {
    private val borderPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        style = Paint.Style.FILL
    }
    private val fillPaint = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        style = Paint.Style.FILL
        color = fillColor
    }
    private val outerRect = RectF()
    private val innerRect = RectF()
    private val shaderMatrix = Matrix()
    private var borderShader: LinearGradient? = null
    private var sweepProgress = 0f

    override fun onBoundsChange(bounds: Rect) {
        super.onBoundsChange(bounds)
        outerRect.set(bounds)
        val inset = borderWidthPx.coerceAtLeast(0f)
        innerRect.set(
            outerRect.left + inset,
            outerRect.top + inset,
            outerRect.right - inset,
            outerRect.bottom - inset,
        )
        buildShader(bounds.width().toFloat().coerceAtLeast(1f))
    }

    fun setSweepProgress(progress: Float) {
        sweepProgress = progress.coerceIn(0f, 1f)
        updateShaderMatrix()
        invalidateSelf()
    }

    private fun buildShader(width: Float) {
        borderShader = LinearGradient(
            -width,
            0f,
            width,
            0f,
            rainbowColors,
            null,
            Shader.TileMode.MIRROR,
        )
        borderPaint.shader = borderShader
        updateShaderMatrix()
    }

    private fun updateShaderMatrix() {
        val shader = borderShader ?: return
        val width = bounds.width().toFloat().coerceAtLeast(1f)
        shaderMatrix.reset()
        shaderMatrix.setTranslate(sweepProgress * width * 2f, 0f)
        shader.setLocalMatrix(shaderMatrix)
    }

    override fun draw(canvas: android.graphics.Canvas) {
        if (!outerRect.isEmpty) {
            canvas.drawRoundRect(outerRect, cornerRadiusPx, cornerRadiusPx, borderPaint)
            canvas.drawRoundRect(
                innerRect,
                (cornerRadiusPx - borderWidthPx).coerceAtLeast(0f),
                (cornerRadiusPx - borderWidthPx).coerceAtLeast(0f),
                fillPaint,
            )
        }
    }

    override fun setAlpha(alpha: Int) {
        borderPaint.alpha = alpha
        fillPaint.alpha = alpha
        invalidateSelf()
    }

    override fun setColorFilter(colorFilter: ColorFilter?) {
        borderPaint.colorFilter = colorFilter
        fillPaint.colorFilter = colorFilter
        invalidateSelf()
    }

    @Deprecated("Deprecated in Java")
    override fun getOpacity(): Int = PixelFormat.TRANSLUCENT
}
