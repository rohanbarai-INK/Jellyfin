using Jellyfin.Database.Implementations.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jellyfin.Database.Implementations.ModelConfiguration
{
    /// <summary>
    /// FluentAPI configuration for the <see cref="ContentRequestWebPushSubscription"/> entity.
    /// </summary>
    public class ContentRequestWebPushSubscriptionConfiguration : IEntityTypeConfiguration<ContentRequestWebPushSubscription>
    {
        /// <inheritdoc/>
        public void Configure(EntityTypeBuilder<ContentRequestWebPushSubscription> builder)
        {
            builder
                .Property(entity => entity.Endpoint)
                .IsRequired()
                .HasMaxLength(2048);

            builder
                .Property(entity => entity.P256dh)
                .IsRequired()
                .HasMaxLength(512);

            builder
                .Property(entity => entity.Auth)
                .IsRequired()
                .HasMaxLength(512);

            builder
                .Property(entity => entity.CreatedAt)
                .IsRequired();

            builder
                .Property(entity => entity.UpdatedAt)
                .IsRequired();

            builder
                .HasIndex(entity => entity.UserId);

            builder
                .HasIndex(entity => entity.Endpoint)
                .IsUnique();

            builder
                .HasOne(entity => entity.User)
                .WithMany()
                .HasForeignKey(entity => entity.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
