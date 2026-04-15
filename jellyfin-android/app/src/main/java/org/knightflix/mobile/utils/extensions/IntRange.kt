@file:Suppress("NOTHING_TO_INLINE")

package org.knightflix.mobile.utils.extensions

import androidx.annotation.CheckResult
import org.knightflix.mobile.utils.Constants

@get:CheckResult
val IntRange.width: Int
    get() = endInclusive - start

@CheckResult
fun IntRange.scaleInRange(percent: Int): Int {
    return start + width * percent / Constants.PERCENT_MAX
}
