using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Jellyfin.Database.Implementations;
using Jellyfin.Database.Implementations.Entities;
using MediaBrowser.Controller;
using MediaBrowser.Controller.ContentRequests;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using WebPush;

namespace Jellyfin.Server.Implementations.ContentRequests
{
    /// <summary>
    /// Stores browser push subscriptions and sends request-completion web push notifications.
    /// </summary>
    public class ContentRequestWebPushService : IContentRequestWebPushService
    {
        private const string _vapidPublicKeyConfigPath = "Notifications:WebPush:VapidPublicKey";
        private const string _vapidPrivateKeyConfigPath = "Notifications:WebPush:VapidPrivateKey";
        private const string _vapidSubjectConfigPath = "Notifications:WebPush:Subject";
        private const string _defaultVapidSubject = "mailto:admin@localhost";
        private const string _persistedVapidFileName = "webpush.vapid.keys.json";

        private readonly IDbContextFactory<JellyfinDbContext> _dbProvider;
        private readonly IConfiguration _configuration;
        private readonly IServerApplicationPaths _applicationPaths;
        private readonly ILogger<ContentRequestWebPushService> _logger;
        private readonly object _vapidLock = new object();

        private VapidKeySet? _cachedVapidKeys;

        /// <summary>
        /// Initializes a new instance of the <see cref="ContentRequestWebPushService"/> class.
        /// </summary>
        /// <param name="dbProvider">Database provider.</param>
        /// <param name="configuration">Application configuration.</param>
        /// <param name="applicationPaths">Application paths.</param>
        /// <param name="logger">Logger.</param>
        public ContentRequestWebPushService(
            IDbContextFactory<JellyfinDbContext> dbProvider,
            IConfiguration configuration,
            IServerApplicationPaths applicationPaths,
            ILogger<ContentRequestWebPushService> logger)
        {
            _dbProvider = dbProvider;
            _configuration = configuration;
            _applicationPaths = applicationPaths;
            _logger = logger;
        }

        /// <inheritdoc />
        public string? GetPublicVapidKey()
        {
            try
            {
                return GetOrCreateVapidKeySet().PublicKey;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to resolve Web Push VAPID public key.");
                return null;
            }
        }

        /// <inheritdoc />
        public async Task UpsertSubscription(Guid userId, string endpoint, string p256dh, string auth)
        {
            if (userId.Equals(Guid.Empty))
            {
                throw new ArgumentException("User id cannot be empty.", nameof(userId));
            }

            if (string.IsNullOrWhiteSpace(endpoint))
            {
                throw new ArgumentException("Endpoint is required.", nameof(endpoint));
            }

            if (string.IsNullOrWhiteSpace(p256dh))
            {
                throw new ArgumentException("p256dh key is required.", nameof(p256dh));
            }

            if (string.IsNullOrWhiteSpace(auth))
            {
                throw new ArgumentException("auth key is required.", nameof(auth));
            }

            var normalizedEndpoint = endpoint.Trim();
            var normalizedP256dh = p256dh.Trim();
            var normalizedAuth = auth.Trim();
            var nowUtc = DateTime.UtcNow;

            var dbContext = await _dbProvider.CreateDbContextAsync().ConfigureAwait(false);
            await using (dbContext.ConfigureAwait(false))
            {
                var matches = await dbContext.ContentRequestWebPushSubscriptions
                    .Where(subscription => subscription.Endpoint == normalizedEndpoint)
                    .OrderByDescending(subscription => subscription.UpdatedAt)
                    .ThenByDescending(subscription => subscription.Id)
                    .ToListAsync()
                    .ConfigureAwait(false);

                if (matches.Count > 1)
                {
                    dbContext.ContentRequestWebPushSubscriptions.RemoveRange(matches.Skip(1));
                }

                var existing = matches.FirstOrDefault();
                if (existing is null)
                {
                    dbContext.ContentRequestWebPushSubscriptions.Add(new ContentRequestWebPushSubscription
                    {
                        UserId = userId,
                        Endpoint = normalizedEndpoint,
                        P256dh = normalizedP256dh,
                        Auth = normalizedAuth,
                        CreatedAt = nowUtc,
                        UpdatedAt = nowUtc,
                        LastNotifiedAt = null
                    });
                }
                else
                {
                    existing.UserId = userId;
                    existing.P256dh = normalizedP256dh;
                    existing.Auth = normalizedAuth;
                    existing.UpdatedAt = nowUtc;
                }

                try
                {
                    await dbContext.SaveChangesAsync().ConfigureAwait(false);
                }
                catch (DbUpdateException)
                {
                    // Another request may have inserted the same endpoint concurrently.
                    // Retry once against the now-existing row to avoid surfacing a 500.
                    var currentRows = await dbContext.ContentRequestWebPushSubscriptions
                        .Where(subscription => subscription.Endpoint == normalizedEndpoint)
                        .OrderByDescending(subscription => subscription.UpdatedAt)
                        .ThenByDescending(subscription => subscription.Id)
                        .ToListAsync()
                        .ConfigureAwait(false);

                    if (currentRows.Count == 0)
                    {
                        throw;
                    }

                    var survivor = currentRows[0];
                    survivor.UserId = userId;
                    survivor.P256dh = normalizedP256dh;
                    survivor.Auth = normalizedAuth;
                    survivor.UpdatedAt = nowUtc;

                    if (currentRows.Count > 1)
                    {
                        dbContext.ContentRequestWebPushSubscriptions.RemoveRange(currentRows.Skip(1));
                    }

                    await dbContext.SaveChangesAsync().ConfigureAwait(false);
                }
            }
        }

        /// <inheritdoc />
        public async Task RemoveSubscription(Guid userId, string endpoint)
        {
            if (userId.Equals(Guid.Empty) || string.IsNullOrWhiteSpace(endpoint))
            {
                return;
            }

            var normalizedEndpoint = endpoint.Trim();

            var dbContext = await _dbProvider.CreateDbContextAsync().ConfigureAwait(false);
            await using (dbContext.ConfigureAwait(false))
            {
                var rows = await dbContext.ContentRequestWebPushSubscriptions
                    .Where(subscription => subscription.UserId.Equals(userId) && subscription.Endpoint == normalizedEndpoint)
                    .ToListAsync()
                    .ConfigureAwait(false);

                if (rows.Count == 0)
                {
                    return;
                }

                dbContext.ContentRequestWebPushSubscriptions.RemoveRange(rows);
                await dbContext.SaveChangesAsync().ConfigureAwait(false);
            }
        }

        /// <inheritdoc />
        public async Task NotifyRequestCompleted(ContentRequestInfo request)
        {
            try
            {
                if (request is null || request.UserId.Equals(Guid.Empty) || !request.JellyfinItemId.HasValue)
                {
                    return;
                }

                if (!TryGetVapidDetails(out var vapidDetails))
                {
                    return;
                }

                var dbContext = await _dbProvider.CreateDbContextAsync().ConfigureAwait(false);
                await using (dbContext.ConfigureAwait(false))
                {
                    var subscriptions = await dbContext.ContentRequestWebPushSubscriptions
                        .Where(subscription => subscription.UserId.Equals(request.UserId))
                        .ToListAsync()
                        .ConfigureAwait(false);

                    if (subscriptions.Count == 0)
                    {
                        return;
                    }

                    var payload = JsonSerializer.Serialize(new
                    {
                        type = "request-content-ready",
                        title = GetNotificationTitle(request),
                        body = GetNotificationBody(request),
                        tag = $"request-content-ready-{request.Id}",
                        data = new
                        {
                            requestId = request.Id,
                            requestContentItemId = request.JellyfinItemId,
                            requestTargetPath = BuildRequestTargetPath(request.JellyfinItemId.Value)
                        }
                    });

                    var client = new WebPushClient();
                    var nowUtc = DateTime.UtcNow;

                    foreach (var subscription in subscriptions)
                    {
                        try
                        {
                            await client.SendNotificationAsync(
                                new PushSubscription(subscription.Endpoint, subscription.P256dh, subscription.Auth),
                                payload,
                                vapidDetails).ConfigureAwait(false);

                            subscription.LastNotifiedAt = nowUtc;
                            subscription.UpdatedAt = nowUtc;
                        }
                        catch (WebPushException ex) when (ex.StatusCode == HttpStatusCode.NotFound || ex.StatusCode == HttpStatusCode.Gone)
                        {
                            dbContext.ContentRequestWebPushSubscriptions.Remove(subscription);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to send request web push notification for request {RequestId}.", request.Id);
                        }
                    }

                    if (dbContext.ChangeTracker.HasChanges())
                    {
                        await dbContext.SaveChangesAsync().ConfigureAwait(false);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Request completion push dispatch failed for request {RequestId}.", request?.Id);
            }
        }

        private bool TryGetVapidDetails(out VapidDetails vapidDetails)
        {
            try
            {
                var keySet = GetOrCreateVapidKeySet();
                vapidDetails = new VapidDetails(keySet.Subject, keySet.PublicKey, keySet.PrivateKey);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to resolve Web Push VAPID details.");
                vapidDetails = null!;
                return false;
            }
        }

        private VapidKeySet GetOrCreateVapidKeySet()
        {
            lock (_vapidLock)
            {
                if (_cachedVapidKeys is not null)
                {
                    return _cachedVapidKeys;
                }

                var configuredPublicKey = _configuration[_vapidPublicKeyConfigPath]?.Trim();
                var configuredPrivateKey = _configuration[_vapidPrivateKeyConfigPath]?.Trim();
                var configuredSubject = NormalizeSubject(_configuration[_vapidSubjectConfigPath]);

                if (!string.IsNullOrWhiteSpace(configuredPublicKey) && !string.IsNullOrWhiteSpace(configuredPrivateKey))
                {
                    _cachedVapidKeys = new VapidKeySet(configuredPublicKey, configuredPrivateKey, configuredSubject);
                    return _cachedVapidKeys;
                }

                if (TryLoadPersistedVapidKeys(out var persisted))
                {
                    _cachedVapidKeys = persisted;
                    return _cachedVapidKeys;
                }

                var generated = VapidHelper.GenerateVapidKeys();
                var generatedSet = new VapidKeySet(generated.PublicKey, generated.PrivateKey, configuredSubject);
                TryPersistVapidKeys(generatedSet);
                _cachedVapidKeys = generatedSet;

                _logger.LogInformation("Generated Web Push VAPID keys automatically at {Path}.", GetPersistedVapidFilePath());
                return _cachedVapidKeys;
            }
        }

        private bool TryLoadPersistedVapidKeys(out VapidKeySet keySet)
        {
            keySet = null!;
            var persistedFilePath = GetPersistedVapidFilePath();

            if (!File.Exists(persistedFilePath))
            {
                return false;
            }

            try
            {
                var json = File.ReadAllText(persistedFilePath);
                var payload = JsonSerializer.Deserialize<PersistedVapidKeyPayload>(json);
                if (payload is null || string.IsNullOrWhiteSpace(payload.PublicKey) || string.IsNullOrWhiteSpace(payload.PrivateKey))
                {
                    return false;
                }

                keySet = new VapidKeySet(payload.PublicKey.Trim(), payload.PrivateKey.Trim(), NormalizeSubject(payload.Subject));
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load persisted Web Push VAPID keys from {Path}.", persistedFilePath);
                return false;
            }
        }

        private void TryPersistVapidKeys(VapidKeySet keySet)
        {
            try
            {
                var persistedFilePath = GetPersistedVapidFilePath();
                var directory = Path.GetDirectoryName(persistedFilePath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var payload = new PersistedVapidKeyPayload
                {
                    PublicKey = keySet.PublicKey,
                    PrivateKey = keySet.PrivateKey,
                    Subject = keySet.Subject
                };

                var json = JsonSerializer.Serialize(payload);
                File.WriteAllText(persistedFilePath, json);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to persist generated Web Push VAPID keys.");
            }
        }

        private string GetPersistedVapidFilePath()
            => Path.Combine(_applicationPaths.ConfigurationDirectoryPath, _persistedVapidFileName);

        private static string NormalizeSubject(string? subject)
            => string.IsNullOrWhiteSpace(subject) ? _defaultVapidSubject : subject.Trim();

        private static string BuildRequestTargetPath(Guid jellyfinItemId)
            => $"#/details?id={Uri.EscapeDataString(jellyfinItemId.ToString("D"))}";

        private static string GetNotificationTitle(ContentRequestInfo request)
            => request.Type == ContentRequestType.Series
                ? "[SERIES] Request Ready"
                : "[MOVIE] Request Ready";

        private static string GetNotificationBody(ContentRequestInfo request)
        {
            var safeTitle = string.IsNullOrWhiteSpace(request.Title) ? "Requested content" : request.Title.Trim();

            if (request.Type == ContentRequestType.Series && request.SeasonNumber.HasValue && request.SeasonNumber.Value > 0)
            {
                return $"\"{safeTitle}\" is now available. Season {request.SeasonNumber.Value} is ready to stream. Tap to open.";
            }

            return $"\"{safeTitle}\" is now available to stream. Tap to open.";
        }

        private sealed class VapidKeySet
        {
            public VapidKeySet(string publicKey, string privateKey, string subject)
            {
                PublicKey = publicKey;
                PrivateKey = privateKey;
                Subject = subject;
            }

            public string PublicKey { get; }

            public string PrivateKey { get; }

            public string Subject { get; }
        }

        private sealed class PersistedVapidKeyPayload
        {
            public string? PublicKey { get; set; }

            public string? PrivateKey { get; set; }

            public string? Subject { get; set; }
        }
    }
}
