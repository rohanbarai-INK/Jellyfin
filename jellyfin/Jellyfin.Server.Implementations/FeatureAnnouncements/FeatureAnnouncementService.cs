using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Jellyfin.Database.Implementations;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Extensions;
using MediaBrowser.Controller.FeatureAnnouncements;
using Microsoft.EntityFrameworkCore;

using ContractCtaTargetType = MediaBrowser.Controller.FeatureAnnouncements.FeatureAnnouncementCtaTargetType;
using ContractStatus = MediaBrowser.Controller.FeatureAnnouncements.FeatureAnnouncementStatus;
using DbCtaTargetType = Jellyfin.Database.Implementations.Enums.FeatureAnnouncementCtaTargetType;
using DbStatus = Jellyfin.Database.Implementations.Enums.FeatureAnnouncementStatus;

namespace Jellyfin.Server.Implementations.FeatureAnnouncements
{
    /// <inheritdoc />
    public class FeatureAnnouncementService : IFeatureAnnouncementService
    {
        private const string DefaultCampaignId = "leaderboard-launch-2026";
        private const string DefaultHeading = "What's New?";
        private const string DefaultHeroGifSource = "builtin:request-popup-accent";
        private const string DefaultMediaImageSource = "builtin:leaderboard-announcement-preview";
        private const string DefaultCtaLabel = "Check It Out";
        private const string DefaultCloseLabel = "Close";

        private static readonly Regex _slugInvalidCharsRegex = new("[^a-z0-9-]+", RegexOptions.Compiled);
        private static readonly Regex _slugDashCollapseRegex = new("-{2,}", RegexOptions.Compiled);

        private readonly IDbContextFactory<JellyfinDbContext> _dbProvider;
        private volatile bool _tableVerified;

        /// <summary>
        /// Initializes a new instance of the <see cref="FeatureAnnouncementService"/> class.
        /// </summary>
        /// <param name="dbProvider">Database provider.</param>
        public FeatureAnnouncementService(IDbContextFactory<JellyfinDbContext> dbProvider)
        {
            _dbProvider = dbProvider;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<FeatureAnnouncementInfo>> GetAdminAnnouncements()
        {
            var dbContext = await _dbProvider.CreateDbContextAsync().ConfigureAwait(false);
            await using (dbContext.ConfigureAwait(false))
            {
                await EnsureTableReadyAsync(dbContext).ConfigureAwait(false);

                var rows = await dbContext.FeatureAnnouncements
                    .AsNoTracking()
                    .OrderByDescending(announcement => announcement.Priority)
                    .ThenByDescending(announcement => announcement.SortOrder)
                    .ThenByDescending(announcement => announcement.UpdatedAtUtc)
                    .ToListAsync()
                    .ConfigureAwait(false);

                return rows.Select(ToContract).ToList();
            }
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<FeatureAnnouncementInfo>> GetActiveAnnouncements(DateTime nowUtc)
        {
            var dbContext = await _dbProvider.CreateDbContextAsync().ConfigureAwait(false);
            await using (dbContext.ConfigureAwait(false))
            {
                await EnsureTableReadyAsync(dbContext).ConfigureAwait(false);

                var rows = await dbContext.FeatureAnnouncements
                    .AsNoTracking()
                    .Where(announcement => announcement.Enabled
                        && announcement.Status == DbStatus.Published
                        && (!announcement.StartsAtUtc.HasValue || announcement.StartsAtUtc.Value <= nowUtc)
                        && (!announcement.EndsAtUtc.HasValue || announcement.EndsAtUtc.Value >= nowUtc))
                    .OrderByDescending(announcement => announcement.Priority)
                    .ThenByDescending(announcement => announcement.SortOrder)
                    .ThenByDescending(announcement => announcement.UpdatedAtUtc)
                    .ToListAsync()
                    .ConfigureAwait(false);

                return rows.Select(ToContract).ToList();
            }
        }

        /// <inheritdoc />
        public async Task<FeatureAnnouncementInfo> UpsertAnnouncement(FeatureAnnouncementUpsertInfo options, Guid actorUserId)
        {
            ArgumentNullException.ThrowIfNull(options);

            if (actorUserId.IsEmpty())
            {
                throw new ArgumentException("Actor user id cannot be empty.", nameof(actorUserId));
            }

            var dbContext = await _dbProvider.CreateDbContextAsync().ConfigureAwait(false);
            await using (dbContext.ConfigureAwait(false))
            {
                await EnsureTableReadyAsync(dbContext).ConfigureAwait(false);

                var nowUtc = DateTime.UtcNow;
                var actorUsername = await ResolveActorUsernameAsync(dbContext, actorUserId).ConfigureAwait(false);

                FeatureAnnouncement entity;
                var isCreate = !options.Id.HasValue || options.Id.Value.IsEmpty();
                var existingId = options.Id.GetValueOrDefault();

                if (isCreate)
                {
                    entity = new FeatureAnnouncement
                    {
                        Id = Guid.NewGuid(),
                        CreatedAtUtc = nowUtc,
                        CreatedByUserId = actorUserId,
                        CreatedByUsername = actorUsername
                    };

                    dbContext.FeatureAnnouncements.Add(entity);
                }
                else
                {
                    entity = await dbContext.FeatureAnnouncements
                        .FirstOrDefaultAsync(announcement => announcement.Id.Equals(existingId))
                        .ConfigureAwait(false)
                        ?? throw new ArgumentException("Announcement not found.", nameof(options));
                }

                var normalized = NormalizeAndValidateOptions(options, entity.Id);

                var duplicateCampaignId = await dbContext.FeatureAnnouncements
                    .AsNoTracking()
                    .AnyAsync(announcement => announcement.CampaignId == normalized.CampaignId && !announcement.Id.Equals(entity.Id))
                    .ConfigureAwait(false);

                if (duplicateCampaignId)
                {
                    throw new ArgumentException("Campaign id already exists.", nameof(options));
                }

                entity.CampaignId = normalized.CampaignId;
                entity.Enabled = normalized.Enabled;
                entity.Status = normalized.Status;
                entity.Heading = normalized.Heading;
                entity.Title = normalized.Title;
                entity.Subtitle = normalized.Subtitle;
                entity.Description = normalized.Description;
                entity.HighlightsJson = SerializeHighlights(normalized.Highlights);
                entity.HelpText = normalized.HelpText;
                entity.HeroGifSource = normalized.HeroGifSource;
                entity.MediaImageSource = normalized.MediaImageSource;
                entity.MediaImageAlt = normalized.MediaImageAlt;
                entity.MediaImageCaption = normalized.MediaImageCaption;
                entity.CtaLabel = normalized.CtaLabel;
                entity.CtaTargetType = normalized.CtaTargetType;
                entity.CtaTarget = normalized.CtaTarget;
                entity.CloseLabel = normalized.CloseLabel;
                entity.StartsAtUtc = normalized.StartsAtUtc;
                entity.EndsAtUtc = normalized.EndsAtUtc;
                entity.MaxImpressionsPerDay = normalized.MaxImpressionsPerDay;
                entity.MaxImpressionsTotal = normalized.MaxImpressionsTotal;
                entity.Priority = normalized.Priority;
                entity.SortOrder = normalized.SortOrder;
                entity.UpdatedAtUtc = nowUtc;
                entity.UpdatedByUserId = actorUserId;
                entity.UpdatedByUsername = actorUsername;

                if (string.IsNullOrWhiteSpace(entity.CreatedByUsername))
                {
                    entity.CreatedByUserId = actorUserId;
                    entity.CreatedByUsername = actorUsername;
                    entity.CreatedAtUtc = nowUtc;
                }

                await dbContext.SaveChangesAsync().ConfigureAwait(false);
                return ToContract(entity);
            }
        }

        private static NormalizedAnnouncement NormalizeAndValidateOptions(FeatureAnnouncementUpsertInfo options, Guid entityId)
        {
            var normalizedTitle = NormalizeRequiredText(options.Title, nameof(options.Title), 180);
            var normalizedDescription = NormalizeRequiredText(options.Description, nameof(options.Description), 2000);
            var normalizedHeading = NormalizeOptionalText(options.Heading, DefaultHeading, 120);
            var normalizedSubtitle = NormalizeOptionalText(options.Subtitle, string.Empty, 350);
            var normalizedHelpText = NormalizeOptionalText(options.HelpText, string.Empty, 1000);
            var normalizedMediaAlt = NormalizeOptionalText(options.MediaImageAlt, "Announcement media preview", 255);
            var normalizedMediaCaption = NormalizeOptionalText(options.MediaImageCaption, string.Empty, 500);
            var normalizedCtaLabel = NormalizeOptionalText(options.CtaLabel, DefaultCtaLabel, 100);
            var normalizedCloseLabel = NormalizeOptionalText(options.CloseLabel, DefaultCloseLabel, 100);

            var normalizedCampaignId = NormalizeCampaignId(options.CampaignId, normalizedTitle, entityId);
            var normalizedHeroGifSource = NormalizeOptionalText(options.HeroGifSource, DefaultHeroGifSource, 8192);
            var normalizedMediaImageSource = NormalizeOptionalText(options.MediaImageSource, DefaultMediaImageSource, 5242880);
            var normalizedCtaTarget = NormalizeOptionalText(options.CtaTarget, string.Empty, 1024);

            if (options.StartsAtUtc.HasValue && options.EndsAtUtc.HasValue && options.StartsAtUtc.Value > options.EndsAtUtc.Value)
            {
                throw new ArgumentException("Start date must be earlier than or equal to end date.", nameof(options));
            }

            if (options.MaxImpressionsPerDay <= 0)
            {
                throw new ArgumentException("Max impressions per day must be greater than zero.", nameof(options));
            }

            if (options.MaxImpressionsTotal <= 0)
            {
                throw new ArgumentException("Max total impressions must be greater than zero.", nameof(options));
            }

            ValidateCtaTarget(options.CtaTargetType, normalizedCtaTarget);

            var normalizedHighlights = NormalizeHighlights(options.Highlights);

            return new NormalizedAnnouncement(
                normalizedCampaignId,
                options.Enabled,
                ToDbStatus(options.Status),
                normalizedHeading,
                normalizedTitle,
                normalizedSubtitle,
                normalizedDescription,
                normalizedHighlights,
                normalizedHelpText,
                normalizedHeroGifSource,
                normalizedMediaImageSource,
                normalizedMediaAlt,
                normalizedMediaCaption,
                normalizedCtaLabel,
                ToDbCtaTargetType(options.CtaTargetType),
                normalizedCtaTarget,
                normalizedCloseLabel,
                options.StartsAtUtc,
                options.EndsAtUtc,
                options.MaxImpressionsPerDay,
                options.MaxImpressionsTotal,
                options.Priority,
                options.SortOrder);
        }

        private static string NormalizeCampaignId(string campaignId, string title, Guid entityId)
        {
            var baseValue = string.IsNullOrWhiteSpace(campaignId) ? title : campaignId;
            var lower = baseValue.Trim().ToLowerInvariant().Replace(' ', '-');
            var slug = _slugInvalidCharsRegex.Replace(lower, "-");
            slug = _slugDashCollapseRegex.Replace(slug, "-").Trim('-');

            if (string.IsNullOrWhiteSpace(slug))
            {
                slug = $"announcement-{entityId:N}";
            }

            if (slug.Length > 120)
            {
                slug = slug.Substring(0, 120).Trim('-');
            }

            return slug;
        }

        private static IReadOnlyList<string> NormalizeHighlights(IReadOnlyList<string> highlights)
        {
            if (highlights is null || highlights.Count == 0)
            {
                return Array.Empty<string>();
            }

            var values = new List<string>(highlights.Count);
            foreach (var highlight in highlights)
            {
                var normalized = NormalizeOptionalText(highlight, string.Empty, 280);
                if (string.IsNullOrWhiteSpace(normalized))
                {
                    continue;
                }

                values.Add(normalized);
                if (values.Count >= 10)
                {
                    break;
                }
            }

            return values;
        }

        private static string NormalizeRequiredText(string value, string fieldName, int maxLength)
        {
            var trimmed = (value ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                throw new ArgumentException($"{fieldName} is required.", fieldName);
            }

            if (trimmed.Length > maxLength)
            {
                throw new ArgumentException($"{fieldName} exceeds maximum length of {maxLength}.", fieldName);
            }

            return trimmed;
        }

        private static string NormalizeOptionalText(string value, string fallback, int maxLength)
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
            if (normalized.Length > maxLength)
            {
                normalized = normalized.Substring(0, maxLength);
            }

            return normalized;
        }

        private static void ValidateCtaTarget(ContractCtaTargetType ctaTargetType, string target)
        {
            if (ctaTargetType == ContractCtaTargetType.InternalRoute)
            {
                if (string.IsNullOrEmpty(target) || target[0] != '/')
                {
                    throw new ArgumentException("Internal CTA target must begin with '/'.", nameof(target));
                }

                return;
            }

            if (!Uri.TryCreate(target, UriKind.Absolute, out var parsed)
                || (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps))
            {
                throw new ArgumentException("External CTA target must be a valid http/https URL.", nameof(target));
            }
        }

        private async Task EnsureTableReadyAsync(JellyfinDbContext dbContext)
        {
            if (_tableVerified)
            {
                return;
            }

            const string Sql = @"
                CREATE TABLE IF NOT EXISTS FeatureAnnouncements (
                    Id TEXT NOT NULL PRIMARY KEY,
                    CampaignId TEXT NOT NULL,
                    Enabled INTEGER NOT NULL DEFAULT 1,
                    Status INTEGER NOT NULL DEFAULT 0,
                    Heading TEXT NOT NULL DEFAULT '',
                    Title TEXT NOT NULL DEFAULT '',
                    Subtitle TEXT NOT NULL DEFAULT '',
                    Description TEXT NOT NULL DEFAULT '',
                    HighlightsJson TEXT NOT NULL DEFAULT '[]',
                    HelpText TEXT NOT NULL DEFAULT '',
                    HeroGifSource TEXT NOT NULL DEFAULT '',
                    MediaImageSource TEXT NOT NULL DEFAULT '',
                    MediaImageAlt TEXT NOT NULL DEFAULT '',
                    MediaImageCaption TEXT NOT NULL DEFAULT '',
                    CtaLabel TEXT NOT NULL DEFAULT 'Check It Out',
                    CtaTargetType INTEGER NOT NULL DEFAULT 0,
                    CtaTarget TEXT NOT NULL DEFAULT '/achievements',
                    CloseLabel TEXT NOT NULL DEFAULT 'Close',
                    StartsAtUtc TEXT NULL,
                    EndsAtUtc TEXT NULL,
                    MaxImpressionsPerDay INTEGER NOT NULL DEFAULT 2,
                    MaxImpressionsTotal INTEGER NOT NULL DEFAULT 10,
                    Priority INTEGER NOT NULL DEFAULT 0,
                    SortOrder INTEGER NOT NULL DEFAULT 0,
                    CreatedAtUtc TEXT NOT NULL,
                    UpdatedAtUtc TEXT NOT NULL,
                    CreatedByUserId TEXT NULL,
                    CreatedByUsername TEXT NOT NULL DEFAULT '',
                    UpdatedByUserId TEXT NULL,
                    UpdatedByUsername TEXT NOT NULL DEFAULT ''
                );
                CREATE UNIQUE INDEX IF NOT EXISTS IX_FeatureAnnouncements_CampaignId ON FeatureAnnouncements (CampaignId);
                CREATE INDEX IF NOT EXISTS IX_FeatureAnnouncements_ActiveWindow ON FeatureAnnouncements (Enabled, Status, StartsAtUtc, EndsAtUtc);
                CREATE INDEX IF NOT EXISTS IX_FeatureAnnouncements_PrioritySort ON FeatureAnnouncements (Priority, SortOrder);
            ";

            const string AlterSql = @"
                ALTER TABLE FeatureAnnouncements ADD COLUMN CampaignId TEXT NOT NULL DEFAULT '';
                ALTER TABLE FeatureAnnouncements ADD COLUMN Enabled INTEGER NOT NULL DEFAULT 1;
                ALTER TABLE FeatureAnnouncements ADD COLUMN Status INTEGER NOT NULL DEFAULT 0;
                ALTER TABLE FeatureAnnouncements ADD COLUMN Heading TEXT NOT NULL DEFAULT '';
                ALTER TABLE FeatureAnnouncements ADD COLUMN Title TEXT NOT NULL DEFAULT '';
                ALTER TABLE FeatureAnnouncements ADD COLUMN Subtitle TEXT NOT NULL DEFAULT '';
                ALTER TABLE FeatureAnnouncements ADD COLUMN Description TEXT NOT NULL DEFAULT '';
                ALTER TABLE FeatureAnnouncements ADD COLUMN HighlightsJson TEXT NOT NULL DEFAULT '[]';
                ALTER TABLE FeatureAnnouncements ADD COLUMN HelpText TEXT NOT NULL DEFAULT '';
                ALTER TABLE FeatureAnnouncements ADD COLUMN HeroGifSource TEXT NOT NULL DEFAULT '';
                ALTER TABLE FeatureAnnouncements ADD COLUMN MediaImageSource TEXT NOT NULL DEFAULT '';
                ALTER TABLE FeatureAnnouncements ADD COLUMN MediaImageAlt TEXT NOT NULL DEFAULT '';
                ALTER TABLE FeatureAnnouncements ADD COLUMN MediaImageCaption TEXT NOT NULL DEFAULT '';
                ALTER TABLE FeatureAnnouncements ADD COLUMN CtaLabel TEXT NOT NULL DEFAULT 'Check It Out';
                ALTER TABLE FeatureAnnouncements ADD COLUMN CtaTargetType INTEGER NOT NULL DEFAULT 0;
                ALTER TABLE FeatureAnnouncements ADD COLUMN CtaTarget TEXT NOT NULL DEFAULT '/achievements';
                ALTER TABLE FeatureAnnouncements ADD COLUMN CloseLabel TEXT NOT NULL DEFAULT 'Close';
                ALTER TABLE FeatureAnnouncements ADD COLUMN StartsAtUtc TEXT NULL;
                ALTER TABLE FeatureAnnouncements ADD COLUMN EndsAtUtc TEXT NULL;
                ALTER TABLE FeatureAnnouncements ADD COLUMN MaxImpressionsPerDay INTEGER NOT NULL DEFAULT 2;
                ALTER TABLE FeatureAnnouncements ADD COLUMN MaxImpressionsTotal INTEGER NOT NULL DEFAULT 10;
                ALTER TABLE FeatureAnnouncements ADD COLUMN Priority INTEGER NOT NULL DEFAULT 0;
                ALTER TABLE FeatureAnnouncements ADD COLUMN SortOrder INTEGER NOT NULL DEFAULT 0;
                ALTER TABLE FeatureAnnouncements ADD COLUMN CreatedAtUtc TEXT NOT NULL DEFAULT '0001-01-01 00:00:00';
                ALTER TABLE FeatureAnnouncements ADD COLUMN UpdatedAtUtc TEXT NOT NULL DEFAULT '0001-01-01 00:00:00';
                ALTER TABLE FeatureAnnouncements ADD COLUMN CreatedByUserId TEXT NULL;
                ALTER TABLE FeatureAnnouncements ADD COLUMN CreatedByUsername TEXT NOT NULL DEFAULT '';
                ALTER TABLE FeatureAnnouncements ADD COLUMN UpdatedByUserId TEXT NULL;
                ALTER TABLE FeatureAnnouncements ADD COLUMN UpdatedByUsername TEXT NOT NULL DEFAULT '';
            ";

            await dbContext.Database.ExecuteSqlRawAsync(Sql).ConfigureAwait(false);

            foreach (var alterLine in AlterSql.Split(";", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (string.IsNullOrWhiteSpace(alterLine))
                {
                    continue;
                }

                try
                {
#pragma warning disable EF1003
                    await dbContext.Database.ExecuteSqlRawAsync(alterLine + ";").ConfigureAwait(false);
#pragma warning restore EF1003
                }
                catch (Microsoft.Data.Sqlite.SqliteException)
                {
                    // Column already exists. Ignore.
                }
            }

            await SeedDefaultAnnouncementAsync(dbContext).ConfigureAwait(false);
            _tableVerified = true;
        }

        private async Task SeedDefaultAnnouncementAsync(JellyfinDbContext dbContext)
        {
            var exists = await dbContext.FeatureAnnouncements
                .AsNoTracking()
                .AnyAsync(announcement => announcement.CampaignId == DefaultCampaignId)
                .ConfigureAwait(false);

            if (exists)
            {
                return;
            }

            var nowUtc = DateTime.UtcNow;
            var defaultAnnouncement = new FeatureAnnouncement
            {
                Id = Guid.NewGuid(),
                CampaignId = DefaultCampaignId,
                Enabled = true,
                Status = DbStatus.Published,
                Heading = DefaultHeading,
                Title = "Leaderboard Is Here",
                Subtitle = "Track progress, compare stats, and climb the season rankings.",
                Description = "The new Leaderboard gives you a competitive view of your Jellyfin activity so you can measure progress and chase the next rank.",
                HighlightsJson = SerializeHighlights(new[]
                {
                    "Explore season rankings across multiple metrics.",
                    "Compare your progress against other members.",
                    "See who is just ahead of you and who is right behind you.",
                    "Open Achievements and switch to the Leaderboard tab."
                }),
                HelpText = "Go to Achievements, then tap Leaderboard to start competing.",
                HeroGifSource = DefaultHeroGifSource,
                MediaImageSource = DefaultMediaImageSource,
                MediaImageAlt = "Leaderboard feature preview screenshot",
                MediaImageCaption = "New leaderboard experience available in Achievements.",
                CtaLabel = DefaultCtaLabel,
                CtaTargetType = DbCtaTargetType.InternalRoute,
                CtaTarget = "/achievements",
                CloseLabel = DefaultCloseLabel,
                StartsAtUtc = new DateTime(2026, 4, 17, 0, 0, 0, DateTimeKind.Utc),
                EndsAtUtc = new DateTime(2026, 4, 30, 23, 59, 59, DateTimeKind.Utc),
                MaxImpressionsPerDay = 2,
                MaxImpressionsTotal = 10,
                Priority = 100,
                SortOrder = 100,
                CreatedAtUtc = nowUtc,
                UpdatedAtUtc = nowUtc,
                CreatedByUsername = "system",
                UpdatedByUsername = "system"
            };

            dbContext.FeatureAnnouncements.Add(defaultAnnouncement);
            await dbContext.SaveChangesAsync().ConfigureAwait(false);
        }

        private async Task<string> ResolveActorUsernameAsync(JellyfinDbContext dbContext, Guid actorUserId)
        {
            var username = await dbContext.Users
                .AsNoTracking()
                .Where(user => user.Id.Equals(actorUserId))
                .Select(user => user.Username)
                .FirstOrDefaultAsync()
                .ConfigureAwait(false);

            return string.IsNullOrWhiteSpace(username) ? "admin" : username;
        }

        private static string SerializeHighlights(IReadOnlyList<string> highlights)
        {
            if (highlights is null || highlights.Count == 0)
            {
                return "[]";
            }

            return JsonSerializer.Serialize(highlights);
        }

        private static IReadOnlyList<string> ParseHighlights(string highlightsJson)
        {
            if (string.IsNullOrWhiteSpace(highlightsJson))
            {
                return Array.Empty<string>();
            }

            try
            {
                var parsed = JsonSerializer.Deserialize<List<string>>(highlightsJson);
                if (parsed is null || parsed.Count == 0)
                {
                    return Array.Empty<string>();
                }

                return parsed
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Select(item => item.Trim())
                    .ToList();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        private static FeatureAnnouncementInfo ToContract(FeatureAnnouncement entity)
            => new()
            {
                Id = entity.Id,
                CampaignId = entity.CampaignId,
                Enabled = entity.Enabled,
                Status = ToContractStatus(entity.Status),
                Heading = entity.Heading,
                Title = entity.Title,
                Subtitle = entity.Subtitle,
                Description = entity.Description,
                Highlights = ParseHighlights(entity.HighlightsJson),
                HelpText = entity.HelpText,
                HeroGifSource = entity.HeroGifSource,
                MediaImageSource = entity.MediaImageSource,
                MediaImageAlt = entity.MediaImageAlt,
                MediaImageCaption = entity.MediaImageCaption,
                CtaLabel = entity.CtaLabel,
                CtaTargetType = ToContractCtaTargetType(entity.CtaTargetType),
                CtaTarget = entity.CtaTarget,
                CloseLabel = entity.CloseLabel,
                StartsAtUtc = entity.StartsAtUtc,
                EndsAtUtc = entity.EndsAtUtc,
                MaxImpressionsPerDay = entity.MaxImpressionsPerDay,
                MaxImpressionsTotal = entity.MaxImpressionsTotal,
                Priority = entity.Priority,
                SortOrder = entity.SortOrder,
                CreatedAtUtc = entity.CreatedAtUtc,
                UpdatedAtUtc = entity.UpdatedAtUtc,
                CreatedByUserId = entity.CreatedByUserId,
                CreatedByUsername = entity.CreatedByUsername,
                UpdatedByUserId = entity.UpdatedByUserId,
                UpdatedByUsername = entity.UpdatedByUsername
            };

        private static ContractStatus ToContractStatus(DbStatus status)
            => status switch
            {
                DbStatus.Published => ContractStatus.Published,
                _ => ContractStatus.Draft
            };

        private static ContractCtaTargetType ToContractCtaTargetType(DbCtaTargetType targetType)
            => targetType switch
            {
                DbCtaTargetType.ExternalUrl => ContractCtaTargetType.ExternalUrl,
                _ => ContractCtaTargetType.InternalRoute
            };

        private static DbStatus ToDbStatus(ContractStatus status)
            => status switch
            {
                ContractStatus.Published => DbStatus.Published,
                _ => DbStatus.Draft
            };

        private static DbCtaTargetType ToDbCtaTargetType(ContractCtaTargetType targetType)
            => targetType switch
            {
                ContractCtaTargetType.ExternalUrl => DbCtaTargetType.ExternalUrl,
                _ => DbCtaTargetType.InternalRoute
            };

        private sealed record NormalizedAnnouncement(
            string CampaignId,
            bool Enabled,
            DbStatus Status,
            string Heading,
            string Title,
            string Subtitle,
            string Description,
            IReadOnlyList<string> Highlights,
            string HelpText,
            string HeroGifSource,
            string MediaImageSource,
            string MediaImageAlt,
            string MediaImageCaption,
            string CtaLabel,
            DbCtaTargetType CtaTargetType,
            string CtaTarget,
            string CloseLabel,
            DateTime? StartsAtUtc,
            DateTime? EndsAtUtc,
            int MaxImpressionsPerDay,
            int MaxImpressionsTotal,
            int Priority,
            int SortOrder);
    }
}
