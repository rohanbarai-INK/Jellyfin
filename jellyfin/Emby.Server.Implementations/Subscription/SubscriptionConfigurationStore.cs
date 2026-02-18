using MediaBrowser.Common.Configuration;
using MediaBrowser.Model.Configuration;

namespace Emby.Server.Implementations.Subscription;

/// <summary>
/// A configuration store for subscription pricing settings.
/// </summary>
public class SubscriptionConfigurationStore : ConfigurationStore
{
    /// <summary>
    /// The name of the configuration in storage.
    /// </summary>
    public const string StoreKey = "subscription";

    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionConfigurationStore"/> class.
    /// </summary>
    public SubscriptionConfigurationStore()
    {
        ConfigurationType = typeof(SubscriptionConfiguration);
        Key = StoreKey;
    }
}
