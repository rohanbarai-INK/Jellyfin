package org.knightflix.mobile.player.ui.playermenuhelper

import org.knightflix.mobile.player.ui.ChapterMarking

class ChapterMarkings {
    var markings: List<ChapterMarking> = emptyList()
        private set

    fun setMarkings(markings: List<ChapterMarking>) {
        this.markings = markings
    }
}
