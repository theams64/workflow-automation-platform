using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Api.Data.Configurations
{
    public sealed class IdentityRoleConfiguration : IEntityTypeConfiguration<IdentityRole<int>>
    {
        public void Configure(EntityTypeBuilder<IdentityRole<int>> builder)
        {
            builder.ToTable("asp_net_roles");

            builder.Property(x => x.Id).HasColumnName("id");
            builder.Property(x => x.Name).HasColumnName("name");
            builder.Property(x => x.NormalizedName).HasColumnName("normalized_name");
            builder.Property(x => x.ConcurrencyStamp).HasColumnName("concurrency_stamp");
        }
    }
}
