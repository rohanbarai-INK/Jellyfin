using Jellyfin.Database.Implementations.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jellyfin.Database.Implementations.ModelConfiguration
{
    /// <summary>
    /// FluentAPI configuration for the <see cref="ContentRequest"/> entity.
    /// </summary>
    public class ContentRequestConfiguration : IEntityTypeConfiguration<ContentRequest>
    {
        /// <inheritdoc/>
        public void Configure(EntityTypeBuilder<ContentRequest> builder)
        {
            builder
                .Property(entity => entity.Title)
                .IsRequired()
                .HasMaxLength(255);

            builder
                .Property(entity => entity.NormalizedTitle)
                .IsRequired()
                .HasMaxLength(255);

            builder
                .Property(entity => entity.NotificationCount)
                .HasDefaultValue(0);

            builder
                .Property(entity => entity.IsAdminViewed)
                .HasDefaultValue(false);

            builder
                .Property(entity => entity.CoinRedeemCost)
                .HasDefaultValue(0);

            builder
                .HasIndex(entity => entity.UserId);

            builder
                .HasIndex(entity => entity.Status);

            builder
                .HasIndex(entity => entity.IsAdminViewed);

            builder
                .HasIndex(entity => new { entity.UserId, entity.Type, entity.Status });

            builder
                .HasIndex(entity => entity.NormalizedTitle);

            builder
                .HasIndex(entity => new { entity.UserId, entity.CoinRedeemCost });

            builder
                .HasOne(entity => entity.User)
                .WithMany()
                .HasForeignKey(entity => entity.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
