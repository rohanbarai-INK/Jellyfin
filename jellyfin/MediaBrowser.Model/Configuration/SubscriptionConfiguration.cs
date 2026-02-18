namespace MediaBrowser.Model.Configuration;

/// <summary>
/// Subscription pricing configuration.
/// </summary>
public class SubscriptionConfiguration
{
    /// <summary>
    /// Gets or sets the one month plan price in rupees.
    /// </summary>
    public int OneMonthPrice { get; set; } = 100;

    /// <summary>
    /// Gets or sets the three month plan price in rupees.
    /// </summary>
    public int ThreeMonthPrice { get; set; } = 250;

    /// <summary>
    /// Gets or sets the six month plan price in rupees.
    /// </summary>
    public int SixMonthPrice { get; set; } = 450;

    /// <summary>
    /// Gets or sets the twelve month plan price in rupees.
    /// </summary>
    public int TwelveMonthPrice { get; set; } = 850;
}
