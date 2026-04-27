using System;
using System.IO;
using System.Threading.Tasks;
using Jellyfin.Server.Implementations.AutoHeal;
using MediaBrowser.Controller;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Jellyfin.Server.Tests.AutoHeal;

public class MediaMountAutoHealServiceTests : IDisposable
{
    private readonly string _tempRoot;

    public MediaMountAutoHealServiceTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "knightflix-autoheal-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    [Fact]
    public async Task GetStatusAsync_WhenMediaDirectoriesMissing_TransitionsToDegradedAfterDelay()
    {
        using var environment = new AutoHealEnvironmentScope(new System.Collections.Generic.Dictionary<string, string?>()
        {
            ["KNIGHTFLIX_AUTOHEAL_DOCKER_SOCKET_PATH"] = Path.Combine(_tempRoot, "missing-docker.sock"),
            ["KNIGHTFLIX_AUTOHEAL_MEDIA1_PATH"] = Path.Combine(_tempRoot, "media1"),
            ["KNIGHTFLIX_AUTOHEAL_MEDIA2_PATH"] = Path.Combine(_tempRoot, "empty-media2"),
            ["KNIGHTFLIX_AUTOHEAL_REQUIRED_PATHS"] = "Anime;TVSeries",
            ["KNIGHTFLIX_AUTOHEAL_RECOVERY_DELAY_SECONDS"] = "1",
            ["KNIGHTFLIX_AUTOHEAL_COOLDOWN_SECONDS"] = "1"
        });

        Directory.CreateDirectory(Path.Combine(_tempRoot, "media1"));
        Directory.CreateDirectory(Path.Combine(_tempRoot, "empty-media2"));

        var service = CreateService();

        var reconnecting = await service.GetStatusAsync();
        Assert.Equal("reconnecting", reconnecting.State);
        Assert.False(reconnecting.IsHealthy);

        await Task.Delay(1500);

        var degraded = await service.GetStatusAsync();
        Assert.Equal("degraded", degraded.State);
        Assert.False(degraded.IsHealthy);
        Assert.Equal("Service is temporarily unavailable. Please try again in 1-2 minutes.", degraded.Message);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempRoot))
            {
                Directory.Delete(_tempRoot, true);
            }
        }
        catch
        {
            // Ignore best-effort test cleanup failures.
        }
    }

    private MediaMountAutoHealService CreateService()
    {
        var mockPaths = new Mock<IServerApplicationPaths>();
        mockPaths.SetupGet(paths => paths.ConfigurationDirectoryPath).Returns(Path.Combine(_tempRoot, "config"));

        Directory.CreateDirectory(mockPaths.Object.ConfigurationDirectoryPath);

        return new MediaMountAutoHealService(
            Mock.Of<ILogger<MediaMountAutoHealService>>(),
            mockPaths.Object,
            TimeProvider.System);
    }

    private sealed class AutoHealEnvironmentScope : IDisposable
    {
        private readonly System.Collections.Generic.Dictionary<string, string?> _previousValues = new();

        public AutoHealEnvironmentScope(System.Collections.Generic.IReadOnlyDictionary<string, string?> values)
        {
            foreach (var pair in values)
            {
                _previousValues[pair.Key] = Environment.GetEnvironmentVariable(pair.Key);
                Environment.SetEnvironmentVariable(pair.Key, pair.Value);
            }
        }

        public void Dispose()
        {
            foreach (var pair in _previousValues)
            {
                Environment.SetEnvironmentVariable(pair.Key, pair.Value);
            }
        }
    }
}
