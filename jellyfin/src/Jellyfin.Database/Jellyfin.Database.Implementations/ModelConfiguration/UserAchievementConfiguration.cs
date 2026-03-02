using Jellyfin.Database.Implementations.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jellyfin.Database.Implementations.ModelConfiguration
{
    /// <summary>
    /// FluentAPI configuration for <see cref="UserAchievement"/>.
    /// </summary>
    public class UserAchievementConfiguration : IEntityTypeConfiguration<UserAchievement>
    {
        /// <inheritdoc />
        public void Configure(EntityTypeBuilder<UserAchievement> builder)
        {
            builder
                .Property(entity => entity.AchievementId)
                .IsRequired()
                .HasMaxLength(128);

            builder
                .HasIndex(entity => new { entity.UserId, entity.AchievementId })
                .IsUnique()
                .HasFilter("\"SeasonYear\" IS NULL")
                .HasDatabaseName("IX_UserAchievements_UserId_AchievementId_Permanent");

            builder
                .HasIndex(entity => new { entity.UserId, entity.AchievementId, entity.SeasonYear })
                .IsUnique()
                .HasFilter("\"SeasonYear\" IS NOT NULL")
                .HasDatabaseName("IX_UserAchievements_UserId_AchievementId_SeasonYear");

            builder
                .HasIndex(entity => new { entity.UserId, entity.UnlockedAtUtc });

            builder
                .HasOne(entity => entity.User)
                .WithMany()
                .HasForeignKey(entity => entity.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder
                .HasOne(entity => entity.AchievementDefinition)
                .WithMany()
                .HasForeignKey(entity => entity.AchievementId)
                .HasPrincipalKey(entity => entity.Id)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
