using Jellyfin.Database.Implementations.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jellyfin.Database.Implementations.ModelConfiguration
{
    /// <summary>
    /// FluentAPI configuration for the <see cref="ContentRequestRewardBalance"/> entity.
    /// </summary>
    public class ContentRequestRewardBalanceConfiguration : IEntityTypeConfiguration<ContentRequestRewardBalance>
    {
        /// <inheritdoc/>
        public void Configure(EntityTypeBuilder<ContentRequestRewardBalance> builder)
        {
            builder
                .HasKey(entity => entity.UserId);

            builder
                .Property(entity => entity.MovieCount)
                .HasDefaultValue(0);

            builder
                .Property(entity => entity.SeriesCount)
                .HasDefaultValue(0);

            builder
                .HasOne(entity => entity.User)
                .WithMany()
                .HasForeignKey(entity => entity.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
