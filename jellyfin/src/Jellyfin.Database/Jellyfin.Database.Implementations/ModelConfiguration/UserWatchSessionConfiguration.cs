using Jellyfin.Database.Implementations.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jellyfin.Database.Implementations.ModelConfiguration
{
    /// <summary>
    /// FluentAPI configuration for <see cref="UserWatchSession"/>.
    /// </summary>
    public class UserWatchSessionConfiguration : IEntityTypeConfiguration<UserWatchSession>
    {
        /// <inheritdoc />
        public void Configure(EntityTypeBuilder<UserWatchSession> builder)
        {
            builder
                .Property(entity => entity.SessionId)
                .IsRequired()
                .HasMaxLength(128);

            builder
                .Property(entity => entity.PlaybackSpeed)
                .HasDefaultValue(1D);

            builder
                .Property(entity => entity.IsValidSession)
                .HasDefaultValue(true);

            builder
                .HasIndex(entity => new { entity.UserId, entity.StartTimeUtc });

            builder
                .HasIndex(entity => new { entity.ItemId, entity.StartTimeUtc });

            builder
                .HasIndex(entity => entity.SessionId);
        }
    }
}
