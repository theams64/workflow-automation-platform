using Backend.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Api.Data.Configurations
{
    public sealed class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
    {
        public void Configure(EntityTypeBuilder<UserProfile> builder)
        {
            builder.HasIndex(x => x.IdentityUserId)
                .IsUnique();

            builder.HasOne(x => x.IdentityUser)
                .WithOne()
                .HasForeignKey<UserProfile>(x => x.IdentityUserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
