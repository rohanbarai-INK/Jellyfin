@file:Suppress("NOTHING_TO_INLINE")

package org.knightflix.mobile.utils.extensions

import androidx.fragment.app.Fragment
import org.knightflix.mobile.MainActivity

inline fun Fragment.requireMainActivity(): MainActivity = requireActivity() as MainActivity
