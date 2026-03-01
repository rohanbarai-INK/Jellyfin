using System;
using System.Threading.Tasks;

namespace MediaBrowser.Controller.PersonalInsights
{
    /// <summary>
    /// Provides personal-insight aggregates.
    /// </summary>
    public interface IPersonalInsightsService
    {
        /// <summary>
        /// Gets personal insights for a user and period.
        /// </summary>
        /// <param name="userId">The user id.</param>
        /// <param name="periodType">The requested period type.</param>
        /// <returns>Aggregated personal insights.</returns>
        Task<PersonalInsightsResult> GetInsights(Guid userId, PersonalInsightsPeriodType periodType);
    }
}
