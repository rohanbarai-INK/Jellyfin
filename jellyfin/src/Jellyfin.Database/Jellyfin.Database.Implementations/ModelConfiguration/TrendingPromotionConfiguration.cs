using Jellyfin.Database.Implementations.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jellyfin.Database.Implementations.ModelConfiguration
{
    /// <summary>
    /// FluentAPI configuration for <see cref="TrendingPromotion"/>.
    /// </summary>
    public class TrendingPromotionConfiguration : IEntityTypeConfiguration<TrendingPromotion>
    {
        /// <inheritdoc />
        public void Configure(EntityTypeBuilder<TrendingPromotion> builder)
        {
            builder
                .Property(entity => entity.PromotionId)
                .IsRequired()
                .HasMaxLength(128);

            builder
                .Property(entity => entity.AudienceSegment)
                .IsRequired()
                .HasMaxLength(64)
                .HasDefaultValue("AllUsers");

            builder
                .Property(entity => entity.LabelOverride)
                .IsRequired()
                .HasMaxLength(120)
                .HasDefaultValue(string.Empty);

            builder
                .Property(entity => entity.TaglineOverride)
                .IsRequired()
                .HasMaxLength(350)
                .HasDefaultValue(string.Empty);

            builder
                .Property(entity => entity.ArtworkVariant)
                .IsRequired()
                .HasMaxLength(120)
                .HasDefaultValue(string.Empty);

            builder
                .HasIndex(entity => entity.PromotionId)
                .IsUnique()
                .HasDatabaseName("IX_TrendingPromotions_PromotionId");

            builder
                .HasIndex(entity => new { entity.Enabled, entity.StartsAtUtc, entity.EndsAtUtc })
                .HasDatabaseName("IX_TrendingPromotions_ActiveWindow");

            builder
                .HasIndex(entity => new { entity.ItemId, entity.PinPosition, entity.BoostAmount })
                .HasDatabaseName("IX_TrendingPromotions_ItemPriority");
        }
    }
}
