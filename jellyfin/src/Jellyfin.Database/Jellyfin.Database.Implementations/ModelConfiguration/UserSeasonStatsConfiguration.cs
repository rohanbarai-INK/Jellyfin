using Jellyfin.Database.Implementations.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jellyfin.Database.Implementations.ModelConfiguration
{
    /// <summary>
    /// FluentAPI configuration for <see cref="UserSeasonStats"/>.
    /// </summary>
    public class UserSeasonStatsConfiguration : IEntityTypeConfiguration<UserSeasonStats>
    {
        /// <inheritdoc />
        public void Configure(EntityTypeBuilder<UserSeasonStats> builder)
        {
            builder
                .HasIndex(entity => new { entity.UserId, entity.SeasonYear })
                .IsUnique()
                .HasDatabaseName("IX_UserSeasonStats_UserId_SeasonYear");

            builder
                .HasIndex(entity => new { entity.SeasonYear, entity.TotalXp })
                .HasDatabaseName("IX_UserSeasonStats_SeasonYear_TotalXp");

            builder
                .HasIndex(entity => new { entity.SeasonYear, entity.TotalWatchMinutes })
                .HasDatabaseName("IX_UserSeasonStats_SeasonYear_WatchMinutes");

            builder
                .HasIndex(entity => new { entity.SeasonYear, entity.MoviesCompleted })
                .HasDatabaseName("IX_UserSeasonStats_SeasonYear_Movies");

            builder
                .HasIndex(entity => new { entity.SeasonYear, entity.SeriesCompleted })
                .HasDatabaseName("IX_UserSeasonStats_SeasonYear_Series");

            builder
                .HasIndex(entity => new { entity.SeasonYear, entity.UniqueGenresWatched })
                .HasDatabaseName("IX_UserSeasonStats_SeasonYear_Genres");

            builder
                .HasIndex(entity => new { entity.SeasonYear, entity.CurrentStreakDays })
                .HasDatabaseName("IX_UserSeasonStats_SeasonYear_Streak");

            builder
                .HasIndex(entity => new { entity.SeasonYear, entity.AchievementsUnlocked })
                .HasDatabaseName("IX_UserSeasonStats_SeasonYear_Achievements");

            builder
                .HasIndex(entity => new { entity.SeasonYear, entity.ApprovedRequests })
                .HasDatabaseName("IX_UserSeasonStats_SeasonYear_Requests");

            builder
                .HasOne(entity => entity.User)
                .WithMany()
                .HasForeignKey(entity => entity.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
