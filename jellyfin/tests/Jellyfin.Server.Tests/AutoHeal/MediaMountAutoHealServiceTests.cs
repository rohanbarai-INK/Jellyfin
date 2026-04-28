using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
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
            ["KNIGHTFLIX_AUTOHEAL_COOLDOWN_SECONDS"] = "1",
            ["KNIGHTFLIX_AUTOHEAL_UNHEALTHY_GRACE_SECONDS"] = "0"
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

    [Fact]
    public async Task GetStatusAsync_WhenAtLeastOneRequiredDirectoryExists_RemainsHealthy()
    {
        using var environment = new AutoHealEnvironmentScope(new Dictionary<string, string?>()
        {
            ["KNIGHTFLIX_AUTOHEAL_DOCKER_SOCKET_PATH"] = Path.Combine(_tempRoot, "missing-docker.sock"),
            ["KNIGHTFLIX_AUTOHEAL_MEDIA1_PATH"] = Path.Combine(_tempRoot, "media1"),
            ["KNIGHTFLIX_AUTOHEAL_MEDIA2_PATH"] = Path.Combine(_tempRoot, "media2"),
            ["KNIGHTFLIX_AUTOHEAL_REQUIRED_PATHS"] = "Anime;TVSeries;Hollywood",
            ["KNIGHTFLIX_AUTOHEAL_RECOVERY_DELAY_SECONDS"] = "1",
            ["KNIGHTFLIX_AUTOHEAL_COOLDOWN_SECONDS"] = "1",
            ["KNIGHTFLIX_AUTOHEAL_UNHEALTHY_GRACE_SECONDS"] = "0"
        });

        Directory.CreateDirectory(Path.Combine(_tempRoot, "media1"));
        Directory.CreateDirectory(Path.Combine(_tempRoot, "media2"));
        Directory.CreateDirectory(Path.Combine(_tempRoot, "media2", "TVSeries"));

        var service = CreateService();
        var status = await service.GetStatusAsync();

        Assert.Equal("healthy", status.State);
        Assert.True(status.IsHealthy);
    }

    [Fact]
    public async Task GetStatusAsync_WhenUnhealthyButWithinGrace_DoesNotEnterRecoveryYet()
    {
        using var environment = new AutoHealEnvironmentScope(new Dictionary<string, string?>()
        {
            ["KNIGHTFLIX_AUTOHEAL_DOCKER_SOCKET_PATH"] = Path.Combine(_tempRoot, "missing-docker.sock"),
            ["KNIGHTFLIX_AUTOHEAL_MEDIA1_PATH"] = Path.Combine(_tempRoot, "media1"),
            ["KNIGHTFLIX_AUTOHEAL_MEDIA2_PATH"] = Path.Combine(_tempRoot, "media2"),
            ["KNIGHTFLIX_AUTOHEAL_REQUIRED_PATHS"] = "Anime",
            ["KNIGHTFLIX_AUTOHEAL_RECOVERY_DELAY_SECONDS"] = "1",
            ["KNIGHTFLIX_AUTOHEAL_COOLDOWN_SECONDS"] = "1",
            ["KNIGHTFLIX_AUTOHEAL_UNHEALTHY_GRACE_SECONDS"] = "3"
        });

        Directory.CreateDirectory(Path.Combine(_tempRoot, "media1"));
        Directory.CreateDirectory(Path.Combine(_tempRoot, "media2"));

        var service = CreateService();
        var immediate = await service.GetStatusAsync();

        Assert.Equal("healthy", immediate.State);
        Assert.True(immediate.IsHealthy);
    }

    [Fact]
    public async Task GetStatusAsync_WhenRestartSucceeds_SendsGotifyPreAndPostNotifications()
    {
        using var environment = new AutoHealEnvironmentScope(new Dictionary<string, string?>()
        {
            ["KNIGHTFLIX_AUTOHEAL_DOCKER_SOCKET_PATH"] = Path.Combine(_tempRoot, "missing-docker.sock"),
            ["KNIGHTFLIX_AUTOHEAL_MEDIA1_PATH"] = Path.Combine(_tempRoot, "media1"),
            ["KNIGHTFLIX_AUTOHEAL_MEDIA2_PATH"] = Path.Combine(_tempRoot, "media2"),
            ["KNIGHTFLIX_AUTOHEAL_REQUIRED_PATHS"] = "Anime",
            ["KNIGHTFLIX_AUTOHEAL_RECOVERY_DELAY_SECONDS"] = "1",
            ["KNIGHTFLIX_AUTOHEAL_COOLDOWN_SECONDS"] = "1",
            ["KNIGHTFLIX_AUTOHEAL_UNHEALTHY_GRACE_SECONDS"] = "0",
            ["KNIGHTFLIX_AUTOHEAL_GOTIFY_ENABLED"] = "true",
            ["KNIGHTFLIX_AUTOHEAL_GOTIFY_BASE_URL"] = "https://gotify.example",
            ["KNIGHTFLIX_AUTOHEAL_GOTIFY_TOKEN"] = "token"
        });

        Directory.CreateDirectory(Path.Combine(_tempRoot, "media1"));
        Directory.CreateDirectory(Path.Combine(_tempRoot, "media2"));

        var notifications = new List<string>();
        var service = CreateService(
            restartContainerAction: () =>
            {
                Directory.CreateDirectory(Path.Combine(_tempRoot, "media2", "Anime"));
                return Task.CompletedTask;
            },
            sendGotifyMessageAction: (title, message, _, _) =>
            {
                notifications.Add($"{title}|{message}");
                return Task.CompletedTask;
            });

        var reconnecting = await service.GetStatusAsync();
        Assert.Equal("reconnecting", reconnecting.State);

        await Task.Delay(1600);

        var recovered = await service.GetStatusAsync();
        Assert.Equal("recovered", recovered.State);

        Assert.Equal(2, notifications.Count);
        Assert.Contains("KnightFlix Auto-Heal Restart Triggered (PRE)", notifications[0], StringComparison.Ordinal);
        Assert.Contains("KnightFlix Auto-Heal Restart Completed (SUCCESS)", notifications[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetStatusAsync_WhenRestartFails_SendsGotifyPreAndPostFailureNotifications()
    {
        using var environment = new AutoHealEnvironmentScope(new Dictionary<string, string?>()
        {
            ["KNIGHTFLIX_AUTOHEAL_DOCKER_SOCKET_PATH"] = Path.Combine(_tempRoot, "missing-docker.sock"),
            ["KNIGHTFLIX_AUTOHEAL_MEDIA1_PATH"] = Path.Combine(_tempRoot, "media1"),
            ["KNIGHTFLIX_AUTOHEAL_MEDIA2_PATH"] = Path.Combine(_tempRoot, "media2"),
            ["KNIGHTFLIX_AUTOHEAL_REQUIRED_PATHS"] = "Anime",
            ["KNIGHTFLIX_AUTOHEAL_RECOVERY_DELAY_SECONDS"] = "1",
            ["KNIGHTFLIX_AUTOHEAL_COOLDOWN_SECONDS"] = "1",
            ["KNIGHTFLIX_AUTOHEAL_UNHEALTHY_GRACE_SECONDS"] = "0",
            ["KNIGHTFLIX_AUTOHEAL_GOTIFY_ENABLED"] = "true",
            ["KNIGHTFLIX_AUTOHEAL_GOTIFY_BASE_URL"] = "https://gotify.example",
            ["KNIGHTFLIX_AUTOHEAL_GOTIFY_TOKEN"] = "token"
        });

        Directory.CreateDirectory(Path.Combine(_tempRoot, "media1"));
        Directory.CreateDirectory(Path.Combine(_tempRoot, "media2"));

        var notifications = new List<string>();
        var service = CreateService(
            restartContainerAction: () => throw new InvalidOperationException("forced restart failure"),
            sendGotifyMessageAction: (title, message, _, _) =>
            {
                notifications.Add($"{title}|{message}");
                return Task.CompletedTask;
            });

        var reconnecting = await service.GetStatusAsync();
        Assert.Equal("reconnecting", reconnecting.State);

        await Task.Delay(1600);

        var degraded = await service.GetStatusAsync();
        Assert.Equal("degraded", degraded.State);

        Assert.Equal(2, notifications.Count);
        Assert.Contains("KnightFlix Auto-Heal Restart Triggered (PRE)", notifications[0], StringComparison.Ordinal);
        Assert.Contains("KnightFlix Auto-Heal Restart Completed (FAILED)", notifications[1], StringComparison.Ordinal);
        Assert.Contains("forced restart failure", notifications[1], StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetStatusAsync_WhenGotifyDisabled_DoesNotAttemptGotifySend()
    {
        using var environment = new AutoHealEnvironmentScope(new Dictionary<string, string?>()
        {
            ["KNIGHTFLIX_AUTOHEAL_DOCKER_SOCKET_PATH"] = Path.Combine(_tempRoot, "missing-docker.sock"),
            ["KNIGHTFLIX_AUTOHEAL_MEDIA1_PATH"] = Path.Combine(_tempRoot, "media1"),
            ["KNIGHTFLIX_AUTOHEAL_MEDIA2_PATH"] = Path.Combine(_tempRoot, "media2"),
            ["KNIGHTFLIX_AUTOHEAL_REQUIRED_PATHS"] = "Anime",
            ["KNIGHTFLIX_AUTOHEAL_RECOVERY_DELAY_SECONDS"] = "1",
            ["KNIGHTFLIX_AUTOHEAL_COOLDOWN_SECONDS"] = "1",
            ["KNIGHTFLIX_AUTOHEAL_UNHEALTHY_GRACE_SECONDS"] = "0",
            ["KNIGHTFLIX_AUTOHEAL_GOTIFY_ENABLED"] = "false"
        });

        Directory.CreateDirectory(Path.Combine(_tempRoot, "media1"));
        Directory.CreateDirectory(Path.Combine(_tempRoot, "media2"));

        var gotifySendAttempts = 0;
        var service = CreateService(
            restartContainerAction: () => throw new InvalidOperationException("forced restart failure"),
            sendGotifyMessageAction: (_, _, _, _) =>
            {
                Interlocked.Increment(ref gotifySendAttempts);
                return Task.CompletedTask;
            });

        await service.GetStatusAsync();
        await Task.Delay(1600);
        await service.GetStatusAsync();

        Assert.Equal(0, gotifySendAttempts);
    }

    [Fact]
    public async Task GetStatusAsync_WhenGotifyEnabledButMissingToken_LogsWarningAndContinues()
    {
        using var environment = new AutoHealEnvironmentScope(new Dictionary<string, string?>()
        {
            ["KNIGHTFLIX_AUTOHEAL_DOCKER_SOCKET_PATH"] = Path.Combine(_tempRoot, "missing-docker.sock"),
            ["KNIGHTFLIX_AUTOHEAL_MEDIA1_PATH"] = Path.Combine(_tempRoot, "media1"),
            ["KNIGHTFLIX_AUTOHEAL_MEDIA2_PATH"] = Path.Combine(_tempRoot, "media2"),
            ["KNIGHTFLIX_AUTOHEAL_REQUIRED_PATHS"] = "Anime",
            ["KNIGHTFLIX_AUTOHEAL_RECOVERY_DELAY_SECONDS"] = "1",
            ["KNIGHTFLIX_AUTOHEAL_COOLDOWN_SECONDS"] = "1",
            ["KNIGHTFLIX_AUTOHEAL_UNHEALTHY_GRACE_SECONDS"] = "0",
            ["KNIGHTFLIX_AUTOHEAL_GOTIFY_ENABLED"] = "true",
            ["KNIGHTFLIX_AUTOHEAL_GOTIFY_BASE_URL"] = "https://gotify.example",
            ["KNIGHTFLIX_AUTOHEAL_GOTIFY_TOKEN"] = string.Empty
        });

        Directory.CreateDirectory(Path.Combine(_tempRoot, "media1"));
        Directory.CreateDirectory(Path.Combine(_tempRoot, "media2"));

        var gotifySendAttempts = 0;
        var logger = new Mock<ILogger<MediaMountAutoHealService>>();
        var service = CreateService(
            logger: logger.Object,
            restartContainerAction: () =>
            {
                Directory.CreateDirectory(Path.Combine(_tempRoot, "media2", "Anime"));
                return Task.CompletedTask;
            },
            sendGotifyMessageAction: (_, _, _, _) =>
            {
                Interlocked.Increment(ref gotifySendAttempts);
                return Task.CompletedTask;
            });

        await service.GetStatusAsync();
        await Task.Delay(1600);
        var recovered = await service.GetStatusAsync();

        Assert.Equal("recovered", recovered.State);
        Assert.Equal(0, gotifySendAttempts);
        logger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((value, _) => value.ToString()!.Contains("Gotify is enabled but base URL or token is missing", StringComparison.Ordinal)),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
    }

    [Fact]
    public async Task GetStatusAsync_WhenGotifySendFails_RestartFlowStillContinues()
    {
        using var environment = new AutoHealEnvironmentScope(new Dictionary<string, string?>()
        {
            ["KNIGHTFLIX_AUTOHEAL_DOCKER_SOCKET_PATH"] = Path.Combine(_tempRoot, "missing-docker.sock"),
            ["KNIGHTFLIX_AUTOHEAL_MEDIA1_PATH"] = Path.Combine(_tempRoot, "media1"),
            ["KNIGHTFLIX_AUTOHEAL_MEDIA2_PATH"] = Path.Combine(_tempRoot, "media2"),
            ["KNIGHTFLIX_AUTOHEAL_REQUIRED_PATHS"] = "Anime",
            ["KNIGHTFLIX_AUTOHEAL_RECOVERY_DELAY_SECONDS"] = "1",
            ["KNIGHTFLIX_AUTOHEAL_COOLDOWN_SECONDS"] = "1",
            ["KNIGHTFLIX_AUTOHEAL_UNHEALTHY_GRACE_SECONDS"] = "0",
            ["KNIGHTFLIX_AUTOHEAL_GOTIFY_ENABLED"] = "true",
            ["KNIGHTFLIX_AUTOHEAL_GOTIFY_BASE_URL"] = "https://gotify.example",
            ["KNIGHTFLIX_AUTOHEAL_GOTIFY_TOKEN"] = "token"
        });

        Directory.CreateDirectory(Path.Combine(_tempRoot, "media1"));
        Directory.CreateDirectory(Path.Combine(_tempRoot, "media2"));

        var restartCalls = 0;
        var service = CreateService(
            restartContainerAction: () =>
            {
                restartCalls++;
                Directory.CreateDirectory(Path.Combine(_tempRoot, "media2", "Anime"));
                return Task.CompletedTask;
            },
            sendGotifyMessageAction: (_, _, _, _) => throw new InvalidOperationException("forced gotify failure"));

        await service.GetStatusAsync();
        await Task.Delay(1600);
        var recovered = await service.GetStatusAsync();

        Assert.Equal(1, restartCalls);
        Assert.Equal("recovered", recovered.State);
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

    private MediaMountAutoHealService CreateService(
        ILogger<MediaMountAutoHealService>? logger = null,
        Func<Task>? restartContainerAction = null,
        Func<string, string, int, CancellationToken, Task>? sendGotifyMessageAction = null)
    {
        var mockPaths = new Mock<IServerApplicationPaths>();
        mockPaths.SetupGet(paths => paths.ConfigurationDirectoryPath).Returns(Path.Combine(_tempRoot, "config"));

        Directory.CreateDirectory(mockPaths.Object.ConfigurationDirectoryPath);

        return new MediaMountAutoHealService(
            logger ?? Mock.Of<ILogger<MediaMountAutoHealService>>(),
            mockPaths.Object,
            TimeProvider.System,
            restartContainerAction,
            sendGotifyMessageAction);
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
