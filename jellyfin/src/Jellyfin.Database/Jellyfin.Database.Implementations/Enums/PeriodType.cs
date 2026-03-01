namespace Jellyfin.Database.Implementations.Enums
{
    /// <summary>
    /// The aggregation period type.
    /// </summary>
    public enum PeriodType
    {
        /// <summary>
        /// No period.
        /// </summary>
        None = 0,

        /// <summary>
        /// Monthly aggregation.
        /// </summary>
        Month = 1,

        /// <summary>
        /// Yearly aggregation.
        /// </summary>
        Year = 2,

        /// <summary>
        /// Aggregation across all time.
        /// </summary>
        AllTime = 3
    }
}
