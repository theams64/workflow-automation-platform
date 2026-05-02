using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Api.Data.Configurations
{
    public sealed class IdentityUserLoginConfiguration : IEntityTypeConfiguration<IdentityUserLogin<int>>
    {
        public void Configure(EntityTypeBuilder<IdentityUserLogin<int>> builder) 
        {
            builder.ToTable("asp_net_user_logins");

            builder.Property(x => x.LoginProvider).HasColumnName("login_provider");
            builder.Property(x => x.ProviderKey).HasColumnName("provider_key");
            builder.Property(x => x.ProviderDisplayName).HasColumnName("provider_display_name");
            builder.Property(x => x.UserId).HasColumnName("user_id");
        }
    }
}
