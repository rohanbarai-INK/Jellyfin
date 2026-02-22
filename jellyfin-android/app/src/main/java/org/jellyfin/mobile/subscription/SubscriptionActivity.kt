package org.jellyfin.mobile.subscription

import android.graphics.Paint
import android.os.Bundle
import android.view.View
import android.widget.TextView
import androidx.appcompat.app.AppCompatActivity
import androidx.core.content.ContextCompat
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
        private val PLAN_DAYS = mapOf(
            1 to 30,
            3 to 90,
            6 to 180,
            12 to 365,
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

    private lateinit var binding: ActivitySubscriptionBinding
    private val apiClient: ApiClient by inject()
    private val appPreferences: AppPreferences by inject()
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

        lifecycleScope.launch {
            loadSubscriptionData()
        }
    }

    private suspend fun loadSubscriptionData() {
        val baseUrl = (
            intent.getStringExtra(EXTRA_SERVER_URL)?.takeUnless(String::isBlank)
                ?: apiClient.baseUrl
            )?.trimEnd('/')
        val accessToken = apiClient.accessToken?.takeUnless(String::isBlank)

        if (baseUrl.isNullOrBlank() || accessToken.isNullOrBlank()) {
            Timber.w("Unable to load subscription metadata due to missing server context")
            renderPlans(PricingConfig(3, 100.0, 100.0, 250.0, 450.0, 850.0), null)
            renderCurrentPlanCard(null, 3)
            return
        }

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

    private fun fetchCurrentSubscription(baseUrl: String, accessToken: String): CurrentSubscription? {
        val body = fetchEndpointJson(baseUrl, "Keys/CurrentSubscription", accessToken) ?: return null
        return try {
            val payload = JSONObject(body)
            CurrentSubscription(
                expiryDate = parseInstant(payload.optString("ExpiryDate", null)),
                status = payload.optString("Status", ""),
                isInGracePeriod = payload.optBoolean("IsInGracePeriod", false)
                    || payload.optString("Status", "").equals("Grace", ignoreCase = true),
                graceDaysRemaining = payload.optInt("GraceDaysRemaining", 0).coerceAtLeast(0),
                lastDurationMonths = payload.optInt("LastDurationMonths", -1).takeIf { it in PLAN_DAYS.keys },
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
        val normalizedBaseUrl = baseUrl.trimEnd('/')
        val normalizedPath = path.trimStart('/')
        val endpoint = "$normalizedBaseUrl/$normalizedPath?api_key=${
            URLEncoder.encode(accessToken, StandardCharsets.UTF_8.name())
        }"
        val request = URL(endpoint).openConnection() as HttpURLConnection
        request.requestMethod = "GET"
        request.connectTimeout = 10000
        request.readTimeout = 10000
        request.setRequestProperty("Accept", "application/json")

        return try {
            val statusCode = request.responseCode
            if (statusCode !in 200..299) {
                val errorBody = request.errorStream?.bufferedReader()?.use { it.readText() }
                Timber.w("Request failed for %s with code %d. Body: %s", path, statusCode, errorBody)
                return null
            }

            request.inputStream.bufferedReader().use { it.readText() }
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

    override fun onSupportNavigateUp(): Boolean {
        onBackPressedDispatcher.onBackPressed()
        return true
    }
}
