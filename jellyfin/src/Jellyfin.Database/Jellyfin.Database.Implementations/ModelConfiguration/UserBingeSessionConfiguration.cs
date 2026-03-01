using Jellyfin.Database.Implementations.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Jellyfin.Database.Implementations.ModelConfiguration
{
    /// <summary>
    /// FluentAPI configuration for <see cref="UserBingeSession"/>.
    /// </summary>
    public class UserBingeSessionConfiguration : IEntityTypeConfiguration<UserBingeSession>
    {
        /// <inheritdoc />
        public void Configure(EntityTypeBuilder<UserBingeSession> builder)
        {
            builder
                .HasIndex(entity => new { entity.UserId, entity.SessionDateUtc });
        }
    }
}
