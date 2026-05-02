using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Api.Data.Configurations
{
    public sealed class IdentityUserClaimConfiguration : IEntityTypeConfiguration<IdentityUserClaim<int>>
    {
        public void Configure(EntityTypeBuilder<IdentityUserClaim<int>> builder)
        {
            builder.ToTable("asp_net_user_claims");

            builder.Property(x => x.Id).HasColumnName("id");
            builder.Property(x => x.UserId).HasColumnName("user_id");
            builder.Property(x => x.ClaimType).HasColumnName("claim_type");
            builder.Property(x => x.ClaimValue).HasColumnName("claim_value");
        }
    }
}
