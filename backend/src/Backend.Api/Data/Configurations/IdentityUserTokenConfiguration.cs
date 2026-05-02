using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Api.Data.Configurations
{
    public sealed class IdentityUserTokenConfiguration : IEntityTypeConfiguration<IdentityUserToken<int>>
    {
        public void Configure(EntityTypeBuilder<IdentityUserToken<int>> builder) 
        {
            builder.ToTable("asp_net_user_tokens");

            builder.Property(x => x.UserId).HasColumnName("user_id");
            builder.Property(x => x.LoginProvider).HasColumnName("login_provider");
            builder.Property(x => x.Name).HasColumnName("name");
            builder.Property(x => x.Value).HasColumnName("value");
        }
    }
}
