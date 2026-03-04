using System;
using System.Collections.Generic;

namespace Jellyfin.Api.Models.AccessKeyDtos;

/// <summary>
/// Admin subscription dashboard response.
/// </summary>
public class AdminSubscriptionDashboardResponse
{
    /// <summary>
    /// Gets or sets overview metrics.
    /// </summary>
    public required AdminSubscriptionOverviewResponse Overview { get; set; }

    /// <summary>
    /// Gets or sets expiry radar metrics.
    /// </summary>
    public required AdminSubscriptionExpiryRadarResponse ExpiryRadar { get; set; }

    /// <summary>
    /// Gets or sets key analytics metrics.
    /// </summary>
    public required AdminSubscriptionKeyStatsResponse KeyStats { get; set; }

    /// <summary>
    /// Gets or sets cohort data.
    /// </summary>
    public IReadOnlyList<AdminSubscriptionCohortResponse> Cohorts { get; set; } = Array.Empty<AdminSubscriptionCohortResponse>();

    /// <summary>
    /// Gets or sets system health snapshot.
    /// </summary>
    public required AdminSubscriptionSystemHealthResponse SystemHealth { get; set; }
}
