using Jellyfin.Database.Implementations.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jellyfin.Database.Implementations.ModelConfiguration
{
    /// <summary>
    /// FluentAPI configuration for <see cref="UserPeriodStats"/>.
    /// </summary>
    public class UserPeriodStatsConfiguration : IEntityTypeConfiguration<UserPeriodStats>
    {
        /// <inheritdoc />
        public void Configure(EntityTypeBuilder<UserPeriodStats> builder)
        {
            builder
                .Property(entity => entity.PeriodKey)
                .IsRequired()
                .HasMaxLength(16);

            builder
                .HasIndex(entity => new { entity.UserId, entity.PeriodType, entity.PeriodKey })
                .IsUnique();
        }
    }
}
