namespace MediaBrowser.Controller.Leaderboard
{
    /// <summary>
    /// A neighboring competition row for the current user.
    /// </summary>
    public class LeaderboardCompetitionInfo
    {
        /// <summary>
        /// Gets or sets the row entry.
        /// </summary>
        public LeaderboardEntryInfo Entry { get; set; } = new();

        /// <summary>
        /// Gets or sets the metric gap between the current user and this entry.
        /// </summary>
        public long GapValue { get; set; }

        /// <summary>
        /// Gets or sets the formatted gap label.
        /// </summary>
        public string GapLabel { get; set; } = string.Empty;
    }
}
