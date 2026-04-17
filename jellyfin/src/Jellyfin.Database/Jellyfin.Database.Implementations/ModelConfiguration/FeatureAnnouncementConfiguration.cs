using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Database.Implementations.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jellyfin.Database.Implementations.ModelConfiguration
{
    /// <summary>
    /// FluentAPI configuration for <see cref="FeatureAnnouncement"/>.
    /// </summary>
    public class FeatureAnnouncementConfiguration : IEntityTypeConfiguration<FeatureAnnouncement>
    {
        /// <inheritdoc />
        public void Configure(EntityTypeBuilder<FeatureAnnouncement> builder)
        {
            builder
                .Property(entity => entity.CampaignId)
                .IsRequired()
                .HasMaxLength(128);

            builder
                .Property(entity => entity.Heading)
                .IsRequired()
                .HasMaxLength(120);

            builder
                .Property(entity => entity.Title)
                .IsRequired()
                .HasMaxLength(180);

            builder
                .Property(entity => entity.Description)
                .IsRequired()
                .HasMaxLength(2000);

            builder
                .Property(entity => entity.HighlightsJson)
                .IsRequired()
                .HasDefaultValue("[]");

            builder
                .Property(entity => entity.CtaLabel)
                .IsRequired()
                .HasMaxLength(100)
                .HasDefaultValue("Check It Out");

            builder
                .Property(entity => entity.CtaTarget)
                .IsRequired()
                .HasMaxLength(1024)
                .HasDefaultValue("/achievements");

            builder
                .Property(entity => entity.CloseLabel)
                .IsRequired()
                .HasMaxLength(100)
                .HasDefaultValue("Close");

            builder
                .Property(entity => entity.Status)
                .HasDefaultValue(FeatureAnnouncementStatus.Draft);

            builder
                .Property(entity => entity.CtaTargetType)
                .HasDefaultValue(FeatureAnnouncementCtaTargetType.InternalRoute);

            builder
                .Property(entity => entity.MaxImpressionsPerDay)
                .HasDefaultValue(2);

            builder
                .Property(entity => entity.MaxImpressionsTotal)
                .HasDefaultValue(10);

            builder
                .HasIndex(entity => entity.CampaignId)
                .IsUnique()
                .HasDatabaseName("IX_FeatureAnnouncements_CampaignId");

            builder
                .HasIndex(entity => new { entity.Enabled, entity.Status, entity.StartsAtUtc, entity.EndsAtUtc })
                .HasDatabaseName("IX_FeatureAnnouncements_ActiveWindow");

            builder
                .HasIndex(entity => new { entity.Priority, entity.SortOrder })
                .HasDatabaseName("IX_FeatureAnnouncements_PrioritySort");
        }
    }
}
