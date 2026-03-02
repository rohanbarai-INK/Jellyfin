using Jellyfin.Database.Implementations.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jellyfin.Database.Implementations.ModelConfiguration
{
    /// <summary>
    /// FluentAPI configuration for <see cref="AchievementDefinition"/>.
    /// </summary>
    public class AchievementDefinitionConfiguration : IEntityTypeConfiguration<AchievementDefinition>
    {
        /// <inheritdoc />
        public void Configure(EntityTypeBuilder<AchievementDefinition> builder)
        {
            builder.ToTable("AchievementDefinition");

            builder
                .HasKey(entity => entity.Id);

            builder
                .Property(entity => entity.Id)
                .IsRequired()
                .HasMaxLength(128);

            builder
                .Property(entity => entity.Title)
                .IsRequired()
                .HasMaxLength(128);

            builder
                .Property(entity => entity.Description)
                .IsRequired()
                .HasMaxLength(512);

            builder
                .Property(entity => entity.ImageEmoji)
                .IsRequired()
                .HasMaxLength(16);

            builder
                .Property(entity => entity.Rarity)
                .IsRequired()
                .HasMaxLength(16);

            builder
                .Property(entity => entity.IsSeasonal)
                .HasDefaultValue(false);

            builder
                .Property(entity => entity.SeasonType)
                .HasMaxLength(16);

            builder
                .HasIndex(entity => entity.IsSeasonal);
        }
    }
}
