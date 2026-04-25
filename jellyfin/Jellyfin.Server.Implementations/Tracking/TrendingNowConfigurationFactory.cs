using System.Collections.Generic;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Model.Configuration;

namespace Jellyfin.Server.Implementations.Tracking
{
    /// <summary>
    /// Configuration factory for <see cref="TrendingNowOptions"/>.
    /// </summary>
    public class TrendingNowConfigurationFactory : IConfigurationFactory
    {
        /// <inheritdoc />
        public IEnumerable<ConfigurationStore> GetConfigurations()
        {
            return new[]
            {
                new ConfigurationStore
                {
                    ConfigurationType = typeof(TrendingNowOptions),
                    Key = "trendingnow"
                }
            };
        }
    }
}
