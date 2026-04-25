using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Jellyfin.Database.Implementations;
using Jellyfin.Database.Implementations.Entities;
using MediaBrowser.Controller.Trending;
using Microsoft.EntityFrameworkCore;

namespace Jellyfin.Server.Implementations.Tracking
{
    /// <inheritdoc />
    public class TrendingPromotionService : ITrendingPromotionService
    {
        private static readonly Regex _slugInvalidCharsRegex = new("[^a-z0-9-]+", RegexOptions.Compiled);
        private static readonly Regex _slugDashCollapseRegex = new("-{2,}", RegexOptions.Compiled);

        private readonly IDbContextFactory<JellyfinDbContext> _dbProvider;
        private volatile bool _tableVerified;

        /// <summary>
        /// Initializes a new instance of the <see cref="TrendingPromotionService"/> class.
        /// </summary>
        /// <param name="dbProvider">Database provider.</param>
        public TrendingPromotionService(IDbContextFactory<JellyfinDbContext> dbProvider)
        {
            _dbProvider = dbProvider;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<TrendingPromotionInfo>> GetActivePromotions(DateTime nowUtc)
        {
            var dbContext = await _dbProvider.CreateDbContextAsync().ConfigureAwait(false);
            await using (dbContext.ConfigureAwait(false))
            {
                await EnsureTableReadyAsync(dbContext).ConfigureAwait(false);

                var rows = await dbContext.TrendingPromotions
                    .AsNoTracking()
                    .Where(promotion => promotion.Enabled
                        && (!promotion.StartsAtUtc.HasValue || promotion.StartsAtUtc.Value <= nowUtc)
                        && (!promotion.EndsAtUtc.HasValue || promotion.EndsAtUtc.Value >= nowUtc))
                    .OrderBy(promotion => promotion.PinPosition ?? int.MaxValue)
                    .ThenByDescending(promotion => promotion.BoostAmount)
                    .ThenByDescending(promotion => promotion.UpdatedAtUtc)
                    .ToListAsync()
                    .ConfigureAwait(false);

                return await ToContractsAsync(dbContext, rows).ConfigureAwait(false);
            }
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<TrendingPromotionInfo>> GetAdminPromotions()
        {
            var dbContext = await _dbProvider.CreateDbContextAsync().ConfigureAwait(false);
            await using (dbContext.ConfigureAwait(false))
            {
                await EnsureTableReadyAsync(dbContext).ConfigureAwait(false);

                var rows = await dbContext.TrendingPromotions
                    .AsNoTracking()
                    .OrderBy(promotion => promotion.PinPosition ?? int.MaxValue)
                    .ThenByDescending(promotion => promotion.BoostAmount)
                    .ThenByDescending(promotion => promotion.UpdatedAtUtc)
                    .ToListAsync()
                    .ConfigureAwait(false);

                return await ToContractsAsync(dbContext, rows).ConfigureAwait(false);
            }
        }

        /// <inheritdoc />
        public async Task<TrendingPromotionInfo> UpsertPromotion(TrendingPromotionUpsertInfo options, Guid actorUserId)
        {
            ArgumentNullException.ThrowIfNull(options);

            if (actorUserId == Guid.Empty)
            {
                throw new ArgumentException("Actor user id cannot be empty.", nameof(actorUserId));
            }

            if (options.ItemId == Guid.Empty)
            {
                throw new ArgumentException("Item id is required.", nameof(options));
            }

            var dbContext = await _dbProvider.CreateDbContextAsync().ConfigureAwait(false);
            await using (dbContext.ConfigureAwait(false))
            {
                await EnsureTableReadyAsync(dbContext).ConfigureAwait(false);

                var itemExists = await dbContext.BaseItems
                    .AsNoTracking()
                    .AnyAsync(item => item.Id.Equals(options.ItemId))
                    .ConfigureAwait(false);
                if (!itemExists)
                {
                    throw new ArgumentException("Target item was not found.", nameof(options));
                }

                var nowUtc = DateTime.UtcNow;
                var actorUsername = await ResolveActorUsernameAsync(dbContext, actorUserId).ConfigureAwait(false);

                TrendingPromotion entity;
                var isCreate = !options.Id.HasValue || options.Id.Value == Guid.Empty;
                var existingId = options.Id.GetValueOrDefault();

                if (isCreate)
                {
                    entity = new TrendingPromotion
                    {
                        Id = Guid.NewGuid(),
                        CreatedAtUtc = nowUtc,
                        CreatedByUserId = actorUserId,
                        CreatedByUsername = actorUsername
                    };

                    dbContext.TrendingPromotions.Add(entity);
                }
                else
                {
                    entity = await dbContext.TrendingPromotions
                        .FirstOrDefaultAsync(promotion => promotion.Id.Equals(existingId))
                        .ConfigureAwait(false)
                        ?? throw new ArgumentException("Promotion not found.", nameof(options));
                }

                var normalized = NormalizeAndValidate(options, entity.Id);

                var duplicatePromotionId = await dbContext.TrendingPromotions
                    .AsNoTracking()
                    .AnyAsync(promotion => promotion.PromotionId == normalized.PromotionId && !promotion.Id.Equals(entity.Id))
                    .ConfigureAwait(false);
                if (duplicatePromotionId)
                {
                    throw new ArgumentException("Promotion id already exists.", nameof(options));
                }

                entity.PromotionId = normalized.PromotionId;
                entity.ItemId = normalized.ItemId;
                entity.Enabled = normalized.Enabled;
                entity.StartsAtUtc = normalized.StartsAtUtc;
                entity.EndsAtUtc = normalized.EndsAtUtc;
                entity.PinPosition = normalized.PinPosition;
                entity.BoostAmount = normalized.BoostAmount;
                entity.AudienceSegment = normalized.AudienceSegment.ToString();
                entity.AudienceValue = normalized.AudienceValue;
                entity.LabelOverride = normalized.LabelOverride;
                entity.TaglineOverride = normalized.TaglineOverride;
                entity.ArtworkVariant = normalized.ArtworkVariant;
                entity.UpdatedAtUtc = nowUtc;
                entity.UpdatedByUserId = actorUserId;
                entity.UpdatedByUsername = actorUsername;

                if (string.IsNullOrWhiteSpace(entity.CreatedByUsername))
                {
                    entity.CreatedAtUtc = nowUtc;
                    entity.CreatedByUserId = actorUserId;
                    entity.CreatedByUsername = actorUsername;
                }

                await dbContext.SaveChangesAsync().ConfigureAwait(false);
                return (await ToContractsAsync(dbContext, [entity]).ConfigureAwait(false)).Single();
            }
        }

        /// <inheritdoc />
        public async Task<TrendingPromotionInfo> SetEnabled(Guid promotionId, bool enabled, Guid actorUserId)
        {
            if (promotionId == Guid.Empty)
            {
                throw new ArgumentException("Promotion id is required.", nameof(promotionId));
            }

            if (actorUserId == Guid.Empty)
            {
                throw new ArgumentException("Actor user id cannot be empty.", nameof(actorUserId));
            }

            var dbContext = await _dbProvider.CreateDbContextAsync().ConfigureAwait(false);
            await using (dbContext.ConfigureAwait(false))
            {
                await EnsureTableReadyAsync(dbContext).ConfigureAwait(false);

                var entity = await dbContext.TrendingPromotions
                    .FirstOrDefaultAsync(promotion => promotion.Id.Equals(promotionId))
                    .ConfigureAwait(false)
                    ?? throw new ArgumentException("Promotion not found.", nameof(promotionId));

                entity.Enabled = enabled;
                entity.UpdatedAtUtc = DateTime.UtcNow;
                entity.UpdatedByUserId = actorUserId;
                entity.UpdatedByUsername = await ResolveActorUsernameAsync(dbContext, actorUserId).ConfigureAwait(false);

                await dbContext.SaveChangesAsync().ConfigureAwait(false);
                return (await ToContractsAsync(dbContext, [entity]).ConfigureAwait(false)).Single();
            }
        }

        /// <inheritdoc />
        public async Task DeletePromotion(Guid promotionId)
        {
            if (promotionId == Guid.Empty)
            {
                throw new ArgumentException("Promotion id is required.", nameof(promotionId));
            }

            var dbContext = await _dbProvider.CreateDbContextAsync().ConfigureAwait(false);
            await using (dbContext.ConfigureAwait(false))
            {
                await EnsureTableReadyAsync(dbContext).ConfigureAwait(false);

                var entity = await dbContext.TrendingPromotions
                    .FirstOrDefaultAsync(promotion => promotion.Id.Equals(promotionId))
                    .ConfigureAwait(false);
                if (entity is null)
                {
                    return;
                }

                dbContext.TrendingPromotions.Remove(entity);
                await dbContext.SaveChangesAsync().ConfigureAwait(false);
            }
        }

        private static NormalizedPromotion NormalizeAndValidate(TrendingPromotionUpsertInfo options, Guid entityId)
        {
            var promotionId = NormalizePromotionId(options.PromotionId, entityId);
            var audienceSegment = options.AudienceSegment;
            var audienceValue = NormalizeOptionalText(options.AudienceValue, 128);
            var labelOverride = NormalizeOptionalText(options.LabelOverride, 120);
            var taglineOverride = NormalizeOptionalText(options.TaglineOverride, 350);
            var artworkVariant = NormalizeOptionalText(options.ArtworkVariant, 120);

            if (options.StartsAtUtc.HasValue && options.EndsAtUtc.HasValue && options.StartsAtUtc.Value > options.EndsAtUtc.Value)
            {
                throw new ArgumentException("Start date must be earlier than or equal to end date.", nameof(options));
            }

            if (options.PinPosition.HasValue && options.PinPosition.Value <= 0)
            {
                throw new ArgumentException("Pin position must be greater than zero when provided.", nameof(options));
            }

            if (options.BoostAmount < 0D)
            {
                throw new ArgumentException("Boost amount cannot be negative.", nameof(options));
            }

            if (audienceSegment == TrendingAudienceSegment.TopGenreMatch && string.IsNullOrWhiteSpace(audienceValue))
            {
                throw new ArgumentException("TopGenreMatch promotions require an audience genre.", nameof(options));
            }

            return new NormalizedPromotion(
                promotionId,
                options.ItemId,
                options.Enabled,
                options.StartsAtUtc,
                options.EndsAtUtc,
                options.PinPosition,
                Math.Round(options.BoostAmount, 2, MidpointRounding.AwayFromZero),
                audienceSegment,
                audienceValue,
                labelOverride,
                taglineOverride,
                artworkVariant);
        }

        private static string NormalizePromotionId(string promotionId, Guid entityId)
        {
            var baseValue = string.IsNullOrWhiteSpace(promotionId) ? $"promotion-{entityId:N}" : promotionId.Trim();
            var lower = baseValue.ToLowerInvariant().Replace(' ', '-');
            var slug = _slugInvalidCharsRegex.Replace(lower, "-");
            slug = _slugDashCollapseRegex.Replace(slug, "-").Trim('-');
            if (string.IsNullOrWhiteSpace(slug))
            {
                slug = $"promotion-{entityId:N}";
            }

            return slug.Length > 120 ? slug[..120].Trim('-') : slug;
        }

        private static string NormalizeOptionalText(string value, int maxLength)
        {
            var normalized = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
            return normalized.Length > maxLength ? normalized[..maxLength] : normalized;
        }

        private static TrendingAudienceSegment ParseAudienceSegment(string value)
        {
            if (Enum.TryParse(value, true, out TrendingAudienceSegment segment))
            {
                return segment;
            }

            return TrendingAudienceSegment.AllUsers;
        }

        private async Task EnsureTableReadyAsync(JellyfinDbContext dbContext)
        {
            if (_tableVerified)
            {
                return;
            }

            const string sql = @"
                CREATE TABLE IF NOT EXISTS TrendingPromotions (
                    Id TEXT NOT NULL PRIMARY KEY,
                    PromotionId TEXT NOT NULL,
                    ItemId TEXT NOT NULL,
                    Enabled INTEGER NOT NULL DEFAULT 1,
                    StartsAtUtc TEXT NULL,
                    EndsAtUtc TEXT NULL,
                    PinPosition INTEGER NULL,
                    BoostAmount REAL NOT NULL DEFAULT 0,
                    AudienceSegment TEXT NOT NULL DEFAULT 'AllUsers',
                    AudienceValue TEXT NOT NULL DEFAULT '',
                    LabelOverride TEXT NOT NULL DEFAULT '',
                    TaglineOverride TEXT NOT NULL DEFAULT '',
                    ArtworkVariant TEXT NOT NULL DEFAULT '',
                    CreatedAtUtc TEXT NOT NULL,
                    UpdatedAtUtc TEXT NOT NULL,
                    CreatedByUserId TEXT NULL,
                    CreatedByUsername TEXT NOT NULL DEFAULT '',
                    UpdatedByUserId TEXT NULL,
                    UpdatedByUsername TEXT NOT NULL DEFAULT ''
                );
                CREATE UNIQUE INDEX IF NOT EXISTS IX_TrendingPromotions_PromotionId ON TrendingPromotions (PromotionId);
                CREATE INDEX IF NOT EXISTS IX_TrendingPromotions_ActiveWindow ON TrendingPromotions (Enabled, StartsAtUtc, EndsAtUtc);
                CREATE INDEX IF NOT EXISTS IX_TrendingPromotions_ItemPriority ON TrendingPromotions (ItemId, PinPosition, BoostAmount);
            ";

            const string alterSql = @"
                ALTER TABLE TrendingPromotions ADD COLUMN PromotionId TEXT NOT NULL DEFAULT '';
                ALTER TABLE TrendingPromotions ADD COLUMN ItemId TEXT NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
                ALTER TABLE TrendingPromotions ADD COLUMN Enabled INTEGER NOT NULL DEFAULT 1;
                ALTER TABLE TrendingPromotions ADD COLUMN StartsAtUtc TEXT NULL;
                ALTER TABLE TrendingPromotions ADD COLUMN EndsAtUtc TEXT NULL;
                ALTER TABLE TrendingPromotions ADD COLUMN PinPosition INTEGER NULL;
                ALTER TABLE TrendingPromotions ADD COLUMN BoostAmount REAL NOT NULL DEFAULT 0;
                ALTER TABLE TrendingPromotions ADD COLUMN AudienceSegment TEXT NOT NULL DEFAULT 'AllUsers';
                ALTER TABLE TrendingPromotions ADD COLUMN AudienceValue TEXT NOT NULL DEFAULT '';
                ALTER TABLE TrendingPromotions ADD COLUMN LabelOverride TEXT NOT NULL DEFAULT '';
                ALTER TABLE TrendingPromotions ADD COLUMN TaglineOverride TEXT NOT NULL DEFAULT '';
                ALTER TABLE TrendingPromotions ADD COLUMN ArtworkVariant TEXT NOT NULL DEFAULT '';
                ALTER TABLE TrendingPromotions ADD COLUMN CreatedAtUtc TEXT NOT NULL DEFAULT '0001-01-01 00:00:00';
                ALTER TABLE TrendingPromotions ADD COLUMN UpdatedAtUtc TEXT NOT NULL DEFAULT '0001-01-01 00:00:00';
                ALTER TABLE TrendingPromotions ADD COLUMN CreatedByUserId TEXT NULL;
                ALTER TABLE TrendingPromotions ADD COLUMN CreatedByUsername TEXT NOT NULL DEFAULT '';
                ALTER TABLE TrendingPromotions ADD COLUMN UpdatedByUserId TEXT NULL;
                ALTER TABLE TrendingPromotions ADD COLUMN UpdatedByUsername TEXT NOT NULL DEFAULT '';
            ";

            await dbContext.Database.ExecuteSqlRawAsync(sql).ConfigureAwait(false);

            var existingColumns = await GetExistingColumnsAsync(dbContext, "TrendingPromotions").ConfigureAwait(false);
            foreach (var alterLine in alterSql.Split(";", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (string.IsNullOrWhiteSpace(alterLine))
                {
                    continue;
                }

                if (TryExtractAddedColumnName(alterLine, out var columnName) && existingColumns.Contains(columnName))
                {
                    continue;
                }

                try
                {
                    await dbContext.Database.ExecuteSqlRawAsync(alterLine).ConfigureAwait(false);
                    if (TryExtractAddedColumnName(alterLine, out columnName))
                    {
                        existingColumns.Add(columnName);
                    }
                }
                catch
                {
                    if (!TryExtractAddedColumnName(alterLine, out columnName) || !existingColumns.Contains(columnName))
                    {
                        throw;
                    }
                }
            }

            _tableVerified = true;
        }

        private static async Task<HashSet<string>> GetExistingColumnsAsync(JellyfinDbContext dbContext, string tableName)
        {
            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var connection = dbContext.Database.GetDbConnection();
            var wasClosed = connection.State != System.Data.ConnectionState.Open;
            if (wasClosed)
            {
                await connection.OpenAsync().ConfigureAwait(false);
            }

            await using var command = connection.CreateCommand();
            command.CommandText = $"PRAGMA table_info({tableName})";
            await using var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
            while (await reader.ReadAsync().ConfigureAwait(false))
            {
                var value = reader["name"]?.ToString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    columns.Add(value);
                }
            }

            if (wasClosed)
            {
                await connection.CloseAsync().ConfigureAwait(false);
            }

            return columns;
        }

        private static bool TryExtractAddedColumnName(string alterSql, out string columnName)
        {
            columnName = string.Empty;
            const string marker = "ADD COLUMN";
            var index = alterSql.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                return false;
            }

            var remainder = alterSql[(index + marker.Length)..].Trim();
            if (string.IsNullOrWhiteSpace(remainder))
            {
                return false;
            }

            columnName = remainder
                .Split([' ', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(columnName);
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

        private static TrendingPromotionInfo ToContract(TrendingPromotion entity, string itemTitle)
            => new()
            {
                Id = entity.Id,
                PromotionId = entity.PromotionId,
                ItemId = entity.ItemId,
                ItemTitle = itemTitle,
                Enabled = entity.Enabled,
                StartsAtUtc = entity.StartsAtUtc,
                EndsAtUtc = entity.EndsAtUtc,
                PinPosition = entity.PinPosition,
                BoostAmount = entity.BoostAmount,
                AudienceSegment = ParseAudienceSegment(entity.AudienceSegment),
                AudienceValue = entity.AudienceValue,
                LabelOverride = entity.LabelOverride,
                TaglineOverride = entity.TaglineOverride,
                ArtworkVariant = entity.ArtworkVariant,
                CreatedAtUtc = entity.CreatedAtUtc,
                UpdatedAtUtc = entity.UpdatedAtUtc,
                CreatedByUserId = entity.CreatedByUserId,
                CreatedByUsername = entity.CreatedByUsername,
                UpdatedByUserId = entity.UpdatedByUserId,
                UpdatedByUsername = entity.UpdatedByUsername
            };

        private static async Task<IReadOnlyList<TrendingPromotionInfo>> ToContractsAsync(JellyfinDbContext dbContext, IReadOnlyList<TrendingPromotion> entities)
        {
            if (entities.Count == 0)
            {
                return Array.Empty<TrendingPromotionInfo>();
            }

            var itemTitles = await dbContext.BaseItems
                .AsNoTracking()
                .Where(item => entities.Select(entity => entity.ItemId).Contains(item.Id))
                .Select(item => new { item.Id, Title = item.Name ?? string.Empty })
                .ToDictionaryAsync(item => item.Id, item => item.Title)
                .ConfigureAwait(false);

            return entities
                .Select(entity => ToContract(entity, itemTitles.TryGetValue(entity.ItemId, out var itemTitle) ? itemTitle : string.Empty))
                .ToList();
        }

        private sealed record NormalizedPromotion(
            string PromotionId,
            Guid ItemId,
            bool Enabled,
            DateTime? StartsAtUtc,
            DateTime? EndsAtUtc,
            int? PinPosition,
            double BoostAmount,
            TrendingAudienceSegment AudienceSegment,
            string AudienceValue,
            string LabelOverride,
            string TaglineOverride,
            string ArtworkVariant);
    }
}
