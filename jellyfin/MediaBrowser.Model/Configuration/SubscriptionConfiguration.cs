namespace MediaBrowser.Model.Configuration;

/// <summary>
/// Subscription pricing configuration.
/// </summary>
public class SubscriptionConfiguration
{
    /// <summary>
    /// Gets or sets the number of grace days allowed after subscription expiry.
    /// </summary>
    public int GracePeriodDays { get; set; } = 3;

    /// <summary>
    /// Gets or sets the base monthly price in rupees used for discount calculations.
    /// </summary>
    public decimal BasePricePerMonth { get; set; } = 100;

    /// <summary>
    /// Gets or sets the one month plan price in rupees.
    /// </summary>
    public decimal OneMonthPrice { get; set; } = 100;

    /// <summary>
    /// Gets or sets the three month plan price in rupees.
    /// </summary>
    public decimal ThreeMonthPrice { get; set; } = 250;

    /// <summary>
    /// Gets or sets the six month plan price in rupees.
    /// </summary>
    public decimal SixMonthPrice { get; set; } = 450;

    /// <summary>
    /// Gets or sets the twelve month plan price in rupees.
    /// </summary>
    public decimal TwelveMonthPrice { get; set; } = 850;

    /// <summary>
    /// Gets the plan breakdown exposed for UI clients.
    /// </summary>
    public SubscriptionPlanConfigurationEntry[] Plans
        => new[]
        {
            new SubscriptionPlanConfigurationEntry
            {
                Months = 1,
                Price = OneMonthPrice
            },
            new SubscriptionPlanConfigurationEntry
            {
                Months = 3,
                Price = ThreeMonthPrice
            },
            new SubscriptionPlanConfigurationEntry
            {
                Months = 6,
                Price = SixMonthPrice
            },
            new SubscriptionPlanConfigurationEntry
            {
                Months = 12,
                Price = TwelveMonthPrice
            }
        };
}
