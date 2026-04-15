package org.knightflix.mobile.downloads

import androidx.recyclerview.widget.DiffUtil
import org.knightflix.mobile.data.entity.DownloadEntity

class DownloadDiffCallback : DiffUtil.ItemCallback<DownloadEntity>() {
    override fun areItemsTheSame(oldItem: DownloadEntity, newItem: DownloadEntity): Boolean {
        return oldItem.itemId == newItem.itemId
    }

    override fun areContentsTheSame(oldItem: DownloadEntity, newItem: DownloadEntity): Boolean {
        return oldItem == newItem
    }
}
