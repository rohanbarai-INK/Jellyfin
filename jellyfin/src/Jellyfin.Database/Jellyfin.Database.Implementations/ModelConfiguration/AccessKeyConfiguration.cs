using Jellyfin.Database.Implementations.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jellyfin.Database.Implementations.ModelConfiguration
{
    /// <summary>
    /// FluentAPI configuration for the AccessKey entity.
    /// </summary>
    public class AccessKeyConfiguration : IEntityTypeConfiguration<AccessKey>
    {
        /// <inheritdoc/>
        public void Configure(EntityTypeBuilder<AccessKey> builder)
        {
            builder
                .HasIndex(entity => entity.Key)
                .IsUnique();

            builder
                .Property(entity => entity.RedeemedAmount)
                .HasPrecision(10, 2);

            builder
                .HasOne(entity => entity.RedeemedByUser)
                .WithMany()
                .HasForeignKey(entity => entity.RedeemedByUserId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
