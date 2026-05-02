using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Api.Data.Configurations
{
    public sealed class IdentityRoleClaimConfiguration : IEntityTypeConfiguration<IdentityRoleClaim<int>>
    {
        public void Configure(EntityTypeBuilder<IdentityRoleClaim<int>> builder)
        {
            builder.ToTable("asp_net_role_claims");

            builder.Property(x => x.Id).HasColumnName("id");
            builder.Property(x => x.RoleId).HasColumnName("role_id");
            builder.Property(x => x.ClaimType).HasColumnName("claim_type");
            builder.Property(x => x.ClaimValue).HasColumnName("claim_value");
        }
    }
}
