using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller;
using MediaBrowser.Controller.AutoHeal;
using MediaBrowser.Model.System;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Server.Implementations.AutoHeal;

/// <summary>
/// Handles media mount drift detection and persisted client-facing recovery state.
/// </summary>
public sealed class MediaMountAutoHealService : IMediaMountAutoHealService, IDisposable
{
    private const string StateHealthy = "healthy";
    private const string StateReconnecting = "reconnecting";
    private const string StateRecovered = "recovered";
    private const string StateDegraded = "degraded";

    private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private static readonly HttpClient GotifyHttpClient = new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(5)
    };

    private readonly ILogger<MediaMountAutoHealService> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly string _statusFilePath;
    private readonly string _media1Path;
    private readonly string _media2Path;
    private readonly string[] _requiredRelativePaths;
    private readonly string _containerName;
    private readonly string _dockerSocketPath;
    private readonly bool _gotifyEnabled;
    private readonly string _gotifyBaseUrl;
    private readonly string _gotifyToken;
    private readonly int _gotifyPriority;
    private readonly bool _enabled;
    private readonly TimeSpan _cooldown;
    private readonly TimeSpan _recoveryDelay;
    private readonly TimeSpan _recoveredVisibility;
    private readonly TimeSpan _unhealthyGrace;
    private readonly Func<Task> _restartContainerAction;
    private readonly Func<string, string, int, CancellationToken, Task> _sendGotifyMessageAction;
    private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);
    private Task? _pendingRepairTask;
    private bool _didLogMissingGotifyConfigWarning;

    /// <summary>
    /// Initializes a new instance of the <see cref="MediaMountAutoHealService"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="applicationPaths">The server application paths.</param>
    /// <param name="timeProvider">The time provider.</param>
    /// <param name="restartContainerAction">Optional restart action override for tests.</param>
    /// <param name="sendGotifyMessageAction">Optional Gotify send action override for tests.</param>
    public MediaMountAutoHealService(
        ILogger<MediaMountAutoHealService> logger,
        IServerApplicationPaths applicationPaths,
        TimeProvider timeProvider,
        Func<Task>? restartContainerAction = null,
        Func<string, string, int, CancellationToken, Task>? sendGotifyMessageAction = null)
    {
        _logger = logger;
        _timeProvider = timeProvider;
        _enabled = ReadBoolEnvironment("KNIGHTFLIX_AUTOHEAL_ENABLED", true);
        _containerName = ReadStringEnvironment("KNIGHTFLIX_AUTOHEAL_CONTAINER_NAME", "KnightFlix");
        _dockerSocketPath = ReadStringEnvironment("KNIGHTFLIX_AUTOHEAL_DOCKER_SOCKET_PATH", "/var/run/docker.sock");
        _media1Path = ReadStringEnvironment("KNIGHTFLIX_AUTOHEAL_MEDIA1_PATH", "/media1");
        _media2Path = ReadStringEnvironment("KNIGHTFLIX_AUTOHEAL_MEDIA2_PATH", "/media2");
        _requiredRelativePaths = ReadRequiredPaths();
        _gotifyEnabled = ReadBoolEnvironment("KNIGHTFLIX_AUTOHEAL_GOTIFY_ENABLED", false);
        _gotifyBaseUrl = ReadStringEnvironment("KNIGHTFLIX_AUTOHEAL_GOTIFY_BASE_URL", string.Empty);
        _gotifyToken = ReadStringEnvironment("KNIGHTFLIX_AUTOHEAL_GOTIFY_TOKEN", string.Empty);
        _gotifyPriority = ReadIntEnvironment("KNIGHTFLIX_AUTOHEAL_GOTIFY_PRIORITY", 7, 1);
        _cooldown = TimeSpan.FromSeconds(ReadIntEnvironment("KNIGHTFLIX_AUTOHEAL_COOLDOWN_SECONDS", 600, 30));
        _recoveryDelay = TimeSpan.FromSeconds(ReadIntEnvironment("KNIGHTFLIX_AUTOHEAL_RECOVERY_DELAY_SECONDS", 30, 1));
        _recoveredVisibility = TimeSpan.FromSeconds(ReadIntEnvironment("KNIGHTFLIX_AUTOHEAL_RECOVERED_BANNER_SECONDS", 45, 5));
        _unhealthyGrace = TimeSpan.FromSeconds(ReadIntEnvironment("KNIGHTFLIX_AUTOHEAL_UNHEALTHY_GRACE_SECONDS", 20, 0));
        _restartContainerAction = restartContainerAction ?? RestartContainerAsync;
        _sendGotifyMessageAction = sendGotifyMessageAction ?? SendGotifyMessageAsync;

        var statusDirectory = Path.Combine(applicationPaths.ConfigurationDirectoryPath, "autoheal");
        Directory.CreateDirectory(statusDirectory);
        _statusFilePath = Path.Combine(statusDirectory, "media-mount-status.json");
    }

    /// <inheritdoc />
    public async Task<MediaMountAutoHealStatusInfo> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var now = _timeProvider.GetUtcNow();
            var health = EvaluateHealth();
            var state = await ReadStateAsync(cancellationToken).ConfigureAwait(false);
            state = NormalizeState(state, health, now);

            if (_enabled && !health.IsHealthy && ShouldStartRecovery(state, now))
            {
                state = BeginRecoveryState(state, now, health.FailureReason ?? "Media storage became unavailable.");
                await WriteStateAsync(state, cancellationToken).ConfigureAwait(false);
                QueueRepair(state);
            }
            else
            {
                await PersistIfNeededAsync(state, cancellationToken).ConfigureAwait(false);
            }

            return ToInfo(state);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Releases the service resources.
    /// </summary>
    public void Dispose()
    {
        _gate.Dispose();
    }

    private static bool ReadBoolEnvironment(string name, bool defaultValue)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        return value.Equals("1", StringComparison.OrdinalIgnoreCase)
            || value.Equals("true", StringComparison.OrdinalIgnoreCase)
            || value.Equals("yes", StringComparison.OrdinalIgnoreCase)
            || value.Equals("on", StringComparison.OrdinalIgnoreCase);
    }

    private static int ReadIntEnvironment(string name, int defaultValue, int minValue)
    {
        var value = Environment.GetEnvironmentVariable(name);
        if (!int.TryParse(value, out var parsed))
        {
            return defaultValue;
        }

        return Math.Max(minValue, parsed);
    }

    private static string ReadStringEnvironment(string name, string defaultValue)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? defaultValue : value.Trim();
    }

    private static string[] ReadRequiredPaths()
    {
        var value = Environment.GetEnvironmentVariable("KNIGHTFLIX_AUTOHEAL_REQUIRED_PATHS");
        if (string.IsNullOrWhiteSpace(value))
        {
            return new[] { "Anime", "TVSeries", "Hollywood" };
        }

        return value
            .Split(new[] { ';', ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private (bool IsHealthy, string? FailureReason) EvaluateHealth()
    {
        if (!Directory.Exists(_media1Path))
        {
            return (false, $"Media path '{_media1Path}' is unavailable.");
        }

        if (!Directory.Exists(_media2Path))
        {
            return (false, $"Media path '{_media2Path}' is unavailable.");
        }

        if (_requiredRelativePaths.Length == 0)
        {
            return (true, null);
        }

        var foundRequiredPath = false;
        foreach (var relativePath in _requiredRelativePaths)
        {
            var fullPath = Path.Combine(_media2Path, relativePath);
            if (!Directory.Exists(fullPath))
            {
                continue;
            }

            foundRequiredPath = true;
            break;
        }

        if (!foundRequiredPath)
        {
            return (false, $"None of the required media directories are available under '{_media2Path}'. Expected one of: {string.Join(", ", _requiredRelativePaths)}");
        }

        return (true, null);
    }

    private PersistedState NormalizeState(PersistedState state, (bool IsHealthy, string? FailureReason) health, DateTimeOffset now)
    {
        state.LastCheckedUtc = now;

        if (string.IsNullOrWhiteSpace(state.State))
        {
            state.State = StateHealthy;
        }

        if (health.IsHealthy)
        {
            if (state.FirstUnhealthyDetectedUtc.HasValue)
            {
                state.FirstUnhealthyDetectedUtc = null;
                state.Dirty = true;
            }
        }
        else if (!state.FirstUnhealthyDetectedUtc.HasValue)
        {
            state.FirstUnhealthyDetectedUtc = now;
            state.Dirty = true;
        }

        if (state.State == StateReconnecting)
        {
            if (health.IsHealthy)
            {
                state.State = StateRecovered;
                state.Message = "Playback service has been restored. Please try again.";
                state.FailureReason = null;
                state.LastRecoveredUtc = now;
                state.RecoveredVisibleUntilUtc = now.Add(_recoveredVisibility);
                state.Dirty = true;
                return state;
            }

            if (state.LastRestartRequestedUtc.HasValue && now - state.LastRestartRequestedUtc.Value >= _recoveryDelay)
            {
                state.State = StateDegraded;
                state.Message = "Service is temporarily unavailable. Please try again in 1-2 minutes.";
                state.Dirty = true;
                return state;
            }

            state.Message = "Media storage is reconnecting. Please wait 30 seconds.";
            return state;
        }

        if (state.State == StateRecovered)
        {
            if (!health.IsHealthy)
            {
                if (!HasUnhealthyGraceElapsed(state, now))
                {
                    return state;
                }

                state.State = StateDegraded;
                state.Message = "Service is temporarily unavailable. Please try again in 1-2 minutes.";
                state.FailureReason ??= health.FailureReason;
                state.Dirty = true;
                return state;
            }

            if (state.RecoveredVisibleUntilUtc.HasValue && now >= state.RecoveredVisibleUntilUtc.Value)
            {
                state.State = StateHealthy;
                state.Message = string.Empty;
                state.Dirty = true;
                return state;
            }

            return state;
        }

        if (health.IsHealthy)
        {
            if (state.State != StateHealthy || !string.IsNullOrEmpty(state.Message) || !string.IsNullOrEmpty(state.FailureReason))
            {
                state.State = StateHealthy;
                state.Message = string.Empty;
                state.FailureReason = null;
                state.AttemptCount = 0;
                state.Dirty = true;
            }

            return state;
        }

        if (!HasUnhealthyGraceElapsed(state, now))
        {
            return state;
        }

        state.State = StateDegraded;
        state.Message = "Service is temporarily unavailable. Please try again in 1-2 minutes.";
        state.FailureReason = health.FailureReason;
        state.Dirty = true;
        return state;
    }

    private bool ShouldStartRecovery(PersistedState state, DateTimeOffset now)
    {
        if (!_enabled)
        {
            return false;
        }

        if (state.State == StateReconnecting)
        {
            return false;
        }

        if (state.LastRestartRequestedUtc.HasValue && now - state.LastRestartRequestedUtc.Value < _cooldown)
        {
            return false;
        }

        if (!HasUnhealthyGraceElapsed(state, now))
        {
            return false;
        }

        return _pendingRepairTask is null || _pendingRepairTask.IsCompleted;
    }

    private bool HasUnhealthyGraceElapsed(PersistedState state, DateTimeOffset now)
    {
        if (_unhealthyGrace <= TimeSpan.Zero)
        {
            return true;
        }

        return state.FirstUnhealthyDetectedUtc.HasValue && now - state.FirstUnhealthyDetectedUtc.Value >= _unhealthyGrace;
    }

    private PersistedState BeginRecoveryState(PersistedState existingState, DateTimeOffset now, string failureReason)
    {
        existingState.State = StateReconnecting;
        existingState.Message = "Media storage is reconnecting. Please wait 30 seconds.";
        existingState.FailureReason = failureReason;
        existingState.LastRestartRequestedUtc = now;
        existingState.LastRecoveredUtc = null;
        existingState.RecoveredVisibleUntilUtc = null;
        existingState.AttemptCount = existingState.AttemptCount + 1;
        existingState.Dirty = true;
        return existingState;
    }

    private void QueueRepair(PersistedState stateSnapshot)
    {
        if (_pendingRepairTask is not null && !_pendingRepairTask.IsCompleted)
        {
            return;
        }

        _pendingRepairTask = Task.Run(() => ExecuteRepairAsync(stateSnapshot.AttemptCount, stateSnapshot.FailureReason));
    }

    private async Task ExecuteRepairAsync(int attemptCount, string? failureReason)
    {
        try
        {
            await Task.Delay(TimeSpan.FromMilliseconds(750)).ConfigureAwait(false);
            await SendGotifyPreRestartAsync(attemptCount, failureReason).ConfigureAwait(false);
            await _restartContainerAction().ConfigureAwait(false);
            await SendGotifyPostRestartAsync(attemptCount, true, null).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await SendGotifyPostRestartAsync(attemptCount, false, ex.Message).ConfigureAwait(false);
            _logger.LogError(ex, "Media mount auto-heal repair attempt failed.");

            await _gate.WaitAsync().ConfigureAwait(false);
            try
            {
                var state = await ReadStateAsync(CancellationToken.None).ConfigureAwait(false);
                if (state.State == StateReconnecting && state.AttemptCount == attemptCount)
                {
                    state.State = StateDegraded;
                    state.Message = "Service is temporarily unavailable. Please try again in 1-2 minutes.";
                    state.FailureReason = ex.Message;
                    state.Dirty = true;
                    await WriteStateAsync(state, CancellationToken.None).ConfigureAwait(false);
                }
            }
            finally
            {
                _gate.Release();
            }
        }
    }

    private async Task SendGotifyPreRestartAsync(int attemptCount, string? failureReason)
    {
        if (!CanSendGotify())
        {
            return;
        }

        var title = "KnightFlix Auto-Heal Restart Triggered (PRE)";
        var message = BuildGotifyMessageBody(attemptCount, "PRE_RESTART", failureReason, null);
        await SendGotifyWithWarningBoundaryAsync(title, message).ConfigureAwait(false);
    }

    private async Task SendGotifyPostRestartAsync(int attemptCount, bool succeeded, string? error)
    {
        if (!CanSendGotify())
        {
            return;
        }

        var outcome = succeeded ? "SUCCESS" : "FAILED";
        var title = $"KnightFlix Auto-Heal Restart Completed ({outcome})";
        var message = BuildGotifyMessageBody(attemptCount, $"POST_RESTART_{outcome}", null, error);
        await SendGotifyWithWarningBoundaryAsync(title, message).ConfigureAwait(false);
    }

    private async Task SendGotifyWithWarningBoundaryAsync(string title, string message)
    {
        try
        {
            await _sendGotifyMessageAction(title, message, _gotifyPriority, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send auto-heal Gotify notification.");
        }
    }

    private bool CanSendGotify()
    {
        if (!_gotifyEnabled)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(_gotifyBaseUrl) && !string.IsNullOrWhiteSpace(_gotifyToken))
        {
            return true;
        }

        if (!_didLogMissingGotifyConfigWarning)
        {
            _didLogMissingGotifyConfigWarning = true;
            _logger.LogWarning("Gotify is enabled but base URL or token is missing. Skipping notification send.");
        }

        return false;
    }

    private string BuildGotifyMessageBody(int attemptCount, string phase, string? failureReason, string? error)
    {
        var nowUtc = _timeProvider.GetUtcNow().ToString("O", CultureInfo.InvariantCulture);
        var requiredPaths = _requiredRelativePaths.Length == 0 ? "(none)" : string.Join(", ", _requiredRelativePaths);

        var builder = new StringBuilder();
        builder.Append("phase: ").AppendLine(phase);
        builder.Append("attempt: ").AppendLine(attemptCount.ToString(CultureInfo.InvariantCulture));
        builder.Append("utc: ").AppendLine(nowUtc);
        builder.Append("host: ").AppendLine(Environment.MachineName);
        builder.Append("container: ").AppendLine(_containerName);
        builder.Append("state: ").AppendLine(StateReconnecting);
        builder.Append("media1_path: ").AppendLine(_media1Path);
        builder.Append("media2_path: ").AppendLine(_media2Path);
        builder.Append("required_media2_dirs: ").AppendLine(requiredPaths);

        if (!string.IsNullOrWhiteSpace(failureReason))
        {
            builder.Append("failure_reason: ").AppendLine(failureReason);
        }

        if (!string.IsNullOrWhiteSpace(error))
        {
            builder.Append("error: ").AppendLine(error);
        }

        return builder.ToString().TrimEnd();
    }

    private async Task SendGotifyMessageAsync(string title, string message, int priority, CancellationToken cancellationToken)
    {
        var endpoint = $"{_gotifyBaseUrl.TrimEnd('/')}/message?token={Uri.EscapeDataString(_gotifyToken)}";
        var payload = new GotifyMessageRequest
        {
            Title = title,
            Message = message,
            Priority = priority
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var response = await GotifyHttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Gotify returned status {(int)response.StatusCode}.");
        }
    }

    private async Task RestartContainerAsync()
    {
        if (!File.Exists(_dockerSocketPath))
        {
            throw new InvalidOperationException($"Docker socket not found at '{_dockerSocketPath}'.");
        }

        using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        await socket.ConnectAsync(new UnixDomainSocketEndPoint(_dockerSocketPath)).ConfigureAwait(false);

        using var networkStream = new NetworkStream(socket, ownsSocket: true);
        using var writer = new StreamWriter(networkStream, leaveOpen: true);
        using var reader = new StreamReader(networkStream, leaveOpen: true);

        var request = $"POST /containers/{Uri.EscapeDataString(_containerName)}/restart?t=0 HTTP/1.1\r\nHost: docker\r\nContent-Length: 0\r\nConnection: close\r\n\r\n";
        await writer.WriteAsync(request).ConfigureAwait(false);
        await writer.FlushAsync().ConfigureAwait(false);

        var statusLine = await reader.ReadLineAsync().ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(statusLine))
        {
            throw new InvalidOperationException("Docker restart request returned an empty response.");
        }

        var parts = statusLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2 || !int.TryParse(parts[1], out var statusCode) || statusCode >= 400)
        {
            throw new InvalidOperationException($"Docker restart request failed with response '{statusLine}'.");
        }
    }

    private async Task<PersistedState> ReadStateAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_statusFilePath))
        {
            return new PersistedState();
        }

        try
        {
            await using var stream = File.OpenRead(_statusFilePath);
            var state = await JsonSerializer.DeserializeAsync<PersistedState>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
            return state ?? new PersistedState();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read media mount auto-heal state from {Path}.", _statusFilePath);
            return new PersistedState();
        }
    }

    private async Task PersistIfNeededAsync(PersistedState state, CancellationToken cancellationToken)
    {
        if (!state.Dirty)
        {
            return;
        }

        await WriteStateAsync(state, cancellationToken).ConfigureAwait(false);
    }

    private async Task WriteStateAsync(PersistedState state, CancellationToken cancellationToken)
    {
        state.Dirty = false;
        var tempPath = _statusFilePath + ".tmp";
        await using (var stream = File.Create(tempPath))
        {
            await JsonSerializer.SerializeAsync(stream, state, JsonOptions, cancellationToken).ConfigureAwait(false);
        }

        File.Move(tempPath, _statusFilePath, true);
    }

    private MediaMountAutoHealStatusInfo ToInfo(PersistedState state)
    {
        return new MediaMountAutoHealStatusInfo
        {
            State = state.State,
            Message = state.Message,
            IsHealthy = state.State == StateHealthy || state.State == StateRecovered,
            IsAutoHealEnabled = _enabled,
            IsMockMode = false,
            RetryAfterSeconds = state.State == StateReconnecting ? (int)_recoveryDelay.TotalSeconds : 0,
            FailureReason = state.FailureReason,
            LastCheckedUtc = state.LastCheckedUtc,
            LastRestartRequestedUtc = state.LastRestartRequestedUtc,
            LastRecoveredUtc = state.LastRecoveredUtc
        };
    }

    private sealed class PersistedState
    {
        public string State { get; set; } = StateHealthy;

        public string Message { get; set; } = string.Empty;

        public string? FailureReason { get; set; }

        public DateTimeOffset? LastCheckedUtc { get; set; }

        public DateTimeOffset? LastRestartRequestedUtc { get; set; }

        public DateTimeOffset? LastRecoveredUtc { get; set; }

        public DateTimeOffset? RecoveredVisibleUntilUtc { get; set; }

        public DateTimeOffset? FirstUnhealthyDetectedUtc { get; set; }

        public int AttemptCount { get; set; }

        public bool Dirty { get; set; }
    }

    private sealed class GotifyMessageRequest
    {
        public string Title { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;

        public int Priority { get; set; }
    }
}
