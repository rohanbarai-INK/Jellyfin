using System.Collections.Generic;
using MediaBrowser.Common.Configuration;

namespace Emby.Server.Implementations.Subscription;

/// <summary>
/// Factory for constructing subscription pricing configuration.
/// </summary>
public class SubscriptionConfigurationFactory : IConfigurationFactory
{
    /// <inheritdoc />
    public IEnumerable<ConfigurationStore> GetConfigurations()
    {
        yield return new SubscriptionConfigurationStore();
    }
}
