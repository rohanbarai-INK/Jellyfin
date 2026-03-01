using Jellyfin.Database.Implementations.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jellyfin.Database.Implementations.ModelConfiguration
{
    /// <summary>
    /// FluentAPI configuration for <see cref="UserGenrePeriodStats"/>.
    /// </summary>
    public class UserGenrePeriodStatsConfiguration : IEntityTypeConfiguration<UserGenrePeriodStats>
    {
        /// <inheritdoc />
        public void Configure(EntityTypeBuilder<UserGenrePeriodStats> builder)
        {
            builder
                .Property(entity => entity.PeriodKey)
                .IsRequired()
                .HasMaxLength(16);

            builder
                .Property(entity => entity.GenreId)
                .IsRequired()
                .HasMaxLength(128);

            builder
                .HasIndex(entity => new { entity.UserId, entity.PeriodType, entity.PeriodKey, entity.GenreId })
                .IsUnique();
        }
    }
}
