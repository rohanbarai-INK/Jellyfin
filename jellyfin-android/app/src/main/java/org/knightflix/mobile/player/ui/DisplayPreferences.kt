package org.knightflix.mobile.player.ui

import org.knightflix.mobile.utils.Constants

data class DisplayPreferences(
    val skipBackLength: Long = Constants.DEFAULT_SEEK_TIME_MS,
    val skipForwardLength: Long = Constants.DEFAULT_SEEK_TIME_MS,
)
