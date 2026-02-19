namespace MediaBrowser.Model.Configuration;

/// <summary>
/// Subscription plan pricing entry.
/// </summary>
public class SubscriptionPlanConfigurationEntry
{
    /// <summary>
    /// Gets or sets plan duration in months.
    /// </summary>
    public int Months { get; set; }

    /// <summary>
    /// Gets or sets plan price in rupees.
    /// </summary>
    public decimal Price { get; set; }
}
