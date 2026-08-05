using System;
using System.Linq;
using System.Threading.Tasks;
using Jellyfin.Database.Implementations;
using Jellyfin.Database.Implementations.Entities;
using MediaBrowser.Controller.AppDownload;
using Microsoft.EntityFrameworkCore;

namespace Jellyfin.Server.Implementations.AppDownload
{
    /// <inheritdoc />
    public class AppDownloadService : IAppDownloadService
    {
        private const string SingletonId = "singleton";

        private readonly IDbContextFactory<JellyfinDbContext> _dbProvider;
        private volatile bool _tableVerified;

        /// <summary>
        /// Initializes a new instance of the <see cref="AppDownloadService"/> class.
        /// </summary>
        /// <param name="dbProvider">Database provider.</param>
        public AppDownloadService(IDbContextFactory<JellyfinDbContext> dbProvider)
        {
            _dbProvider = dbProvider;
        }

        /// <inheritdoc />
        public async Task<AppDownloadConfigInfo> GetConfigAsync()
        {
            var dbContext = await _dbProvider.CreateDbContextAsync().ConfigureAwait(false);
            await using (dbContext.ConfigureAwait(false))
            {
                await EnsureTableReadyAsync(dbContext).ConfigureAwait(false);

                var row = await dbContext.AppDownloadConfigs
                    .AsNoTracking()
                    .FirstOrDefaultAsync(r => r.Id == SingletonId)
                    .ConfigureAwait(false);

                return row is null ? new AppDownloadConfigInfo() : ToInfo(row);
            }
        }

        /// <inheritdoc />
        public async Task<AppDownloadConfigInfo> SaveConfigAsync(AppDownloadConfigInfo info, Guid actorUserId)
        {
            var dbContext = await _dbProvider.CreateDbContextAsync().ConfigureAwait(false);
            await using (dbContext.ConfigureAwait(false))
            {
                await EnsureTableReadyAsync(dbContext).ConfigureAwait(false);

                var existing = await dbContext.AppDownloadConfigs
                    .FirstOrDefaultAsync(r => r.Id == SingletonId)
                    .ConfigureAwait(false);

                var nowUtc = DateTime.UtcNow;
                var updatedByUsername = await ResolveActorUsernameAsync(dbContext, actorUserId).ConfigureAwait(false);

                if (existing is null)
                {
                    existing = new AppDownloadConfig { Id = SingletonId };
                    dbContext.AppDownloadConfigs.Add(existing);
                }

                existing.MobileApkUrl = info.MobileApkUrl ?? string.Empty;
                existing.MobileApkFileName = string.IsNullOrWhiteSpace(info.MobileApkFileName)
                    ? "KnightFlix-v0.0.1.apk"
                    : info.MobileApkFileName;
                existing.MobileIsNew = info.MobileIsNew;
                existing.TvApkUrl = info.TvApkUrl ?? string.Empty;
                existing.TvApkFileName = string.IsNullOrWhiteSpace(info.TvApkFileName)
                    ? "KnightFlixTV-v0.0.1.apk"
                    : info.TvApkFileName;
                existing.TvIsNew = info.TvIsNew;
                existing.MaxNewInteractions = info.MaxNewInteractions > 0 ? info.MaxNewInteractions : 3;
                existing.UpdatedAtUtc = nowUtc;
                existing.UpdatedByUsername = updatedByUsername;

                await dbContext.SaveChangesAsync().ConfigureAwait(false);

                return ToInfo(existing);
            }
        }

        private static AppDownloadConfigInfo ToInfo(AppDownloadConfig row)
            => new()
            {
                MobileApkUrl = row.MobileApkUrl,
                MobileApkFileName = row.MobileApkFileName,
                MobileIsNew = row.MobileIsNew,
                TvApkUrl = row.TvApkUrl,
                TvApkFileName = row.TvApkFileName,
                TvIsNew = row.TvIsNew,
                MaxNewInteractions = row.MaxNewInteractions > 0 ? row.MaxNewInteractions : 3,
                UpdatedAtUtc = row.UpdatedAtUtc,
                UpdatedByUsername = row.UpdatedByUsername
            };

        private static async Task<string> ResolveActorUsernameAsync(JellyfinDbContext dbContext, Guid actorUserId)
        {
            var username = await dbContext.Users
                .AsNoTracking()
                .Where(user => user.Id.Equals(actorUserId))
                .Select(user => user.Username)
                .FirstOrDefaultAsync()
                .ConfigureAwait(false);

            return string.IsNullOrWhiteSpace(username) ? "admin" : username;
        }

        private async Task EnsureTableReadyAsync(JellyfinDbContext dbContext)
        {
            if (_tableVerified)
            {
                return;
            }

            const string Sql = @"
                CREATE TABLE IF NOT EXISTS AppDownloadConfigs (
                    Id TEXT NOT NULL PRIMARY KEY,
                    MobileApkUrl TEXT NOT NULL DEFAULT '',
                    MobileApkFileName TEXT NOT NULL DEFAULT 'KnightFlix-v0.0.1.apk',
                    MobileIsNew INTEGER NOT NULL DEFAULT 0,
                    TvApkUrl TEXT NOT NULL DEFAULT '',
                    TvApkFileName TEXT NOT NULL DEFAULT 'KnightFlixTV-v0.0.1.apk',
                    TvIsNew INTEGER NOT NULL DEFAULT 0,
                    MaxNewInteractions INTEGER NOT NULL DEFAULT 3,
                    UpdatedAtUtc TEXT NOT NULL DEFAULT '0001-01-01T00:00:00',
                    UpdatedByUsername TEXT NOT NULL DEFAULT ''
                );
            ";

            const string AlterSql = @"
                ALTER TABLE AppDownloadConfigs ADD COLUMN MaxNewInteractions INTEGER NOT NULL DEFAULT 3;
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
                    // Column already exists or a race in clustered startup. Ignore.
                }
            }

            _tableVerified = true;
        }
    }
}
