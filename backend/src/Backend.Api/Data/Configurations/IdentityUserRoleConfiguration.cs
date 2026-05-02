using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Api.Data.Configurations
{
    public sealed class IdentityUserRoleConfiguration : IEntityTypeConfiguration<IdentityUserRole<int>>
    {
        public void Configure(EntityTypeBuilder<IdentityUserRole<int>> builder) 
        {
            builder.ToTable("asp_net_user_roles");

            builder.Property(x => x.UserId).HasColumnName("user_id");
            builder.Property(x => x.RoleId).HasColumnName("role_id");
        }
    }
}
