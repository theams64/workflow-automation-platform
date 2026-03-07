using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Backend.Api.Models.Entities;
using Backend.Api.Models.Entities.Common;
using Microsoft.AspNetCore.Identity;

namespace Backend.Api.Data
{
    public sealed class AppDbContext : IdentityDbContext<ApplicationUser, IdentityRole<int>, int>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
        public DbSet<Workflow> Workflow { get; set; }
        public DbSet<WorkflowStep> WorkflowStep { get; set; }
        public DbSet<WorkflowRun> WorkflowRun { get; set; }
        public DbSet<StepRun> StepRun { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Application tables
            builder.Entity<UserProfile>()
                .HasIndex(x => x.IdentityUserId)
                .IsUnique();

            builder.Entity<UserProfile>()
                .HasOne(x => x.IdentityUser)
                .WithOne()
                .HasForeignKey<UserProfile>(x => x.IdentityUserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Identity tables
            // Users
            builder.Entity<ApplicationUser>(b =>
            {
                b.ToTable("asp_net_users");

                b.Property(x => x.Id).HasColumnName("id");
                b.Property(x => x.UserName).HasColumnName("user_name");
                b.Property(x => x.NormalizedUserName).HasColumnName("normalized_user_name");
                b.Property(x => x.Email).HasColumnName("email");
                b.Property(x => x.NormalizedEmail).HasColumnName("normalized_email");
                b.Property(x => x.EmailConfirmed).HasColumnName("email_confirmed");
                b.Property(x => x.PasswordHash).HasColumnName("password_hash");
                b.Property(x => x.SecurityStamp).HasColumnName("security_stamp");
                b.Property(x => x.ConcurrencyStamp).HasColumnName("concurrency_stamp");
                b.Property(x => x.PhoneNumber).HasColumnName("phone_number");
                b.Property(x => x.PhoneNumberConfirmed).HasColumnName("phone_number_confirmed");
                b.Property(x => x.TwoFactorEnabled).HasColumnName("two_factor_enabled");
                b.Property(x => x.LockoutEnd).HasColumnName("lockout_end");
                b.Property(x => x.LockoutEnabled).HasColumnName("lockout_enabled");
                b.Property(x => x.AccessFailedCount).HasColumnName("access_failed_count");
            });

            // Roles
            builder.Entity<IdentityRole<int>>(b =>
            {
                b.ToTable("asp_net_roles");

                b.Property(x => x.Id).HasColumnName("id");
                b.Property(x => x.Name).HasColumnName("name");
                b.Property(x => x.NormalizedName).HasColumnName("normalized_name");
                b.Property(x => x.ConcurrencyStamp).HasColumnName("concurrency_stamp");
            });

            // User claims
            builder.Entity<IdentityUserClaim<int>>(b =>
            {
                b.ToTable("asp_net_user_claims");
                b.Property(x => x.Id).HasColumnName("id");
                b.Property(x => x.UserId).HasColumnName("user_id");
                b.Property(x => x.ClaimType).HasColumnName("claim_type");
                b.Property(x => x.ClaimValue).HasColumnName("claim_value");
            });

            // User logins
            builder.Entity<IdentityUserLogin<int>>(b =>
            {
                b.ToTable("asp_net_user_logins");
                b.Property(x => x.LoginProvider).HasColumnName("login_provider");
                b.Property(x => x.ProviderKey).HasColumnName("provider_key");
                b.Property(x => x.ProviderDisplayName).HasColumnName("provider_display_name");
                b.Property(x => x.UserId).HasColumnName("user_id");
            });

            // User tokens
            builder.Entity<IdentityUserToken<int>>(b =>
            {
                b.ToTable("asp_net_user_tokens");
                b.Property(x => x.UserId).HasColumnName("user_id");
                b.Property(x => x.LoginProvider).HasColumnName("login_provider");
                b.Property(x => x.Name).HasColumnName("name");
                b.Property(x => x.Value).HasColumnName("value");
            });

            // Role claims
            builder.Entity<IdentityRoleClaim<int>>(b =>
            {
                b.ToTable("asp_net_role_claims");
                b.Property(x => x.Id).HasColumnName("id");
                b.Property(x => x.RoleId).HasColumnName("role_id");
                b.Property(x => x.ClaimType).HasColumnName("claim_type");
                b.Property(x => x.ClaimValue).HasColumnName("claim_value");
            });

            // User roles
            builder.Entity<IdentityUserRole<int>>(b =>
            {
                b.ToTable("asp_net_user_roles");
                b.Property(x => x.UserId).HasColumnName("user_id");
                b.Property(x => x.RoleId).HasColumnName("role_id");
            });
        }
        
        public override int SaveChanges()
        {
            ApplyAuditInfo();
            return base.SaveChanges();
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            ApplyAuditInfo();
            return base.SaveChangesAsync(cancellationToken); 
        }

        public void ApplyAuditInfo()
        {
            var now = DateTimeOffset.UtcNow;

            foreach (var entry in ChangeTracker.Entries<IAuditable>())
            {
                if (entry.State == EntityState.Added)
                {
                    entry.Entity.CreatedAt = now;
                    entry.Entity.UpdatedAt = now;
                }
                else if (entry.State == EntityState.Modified)
                {
                    entry.Property(x => x.CreatedAt).IsModified = false;
                    entry.Entity.UpdatedAt = now;
                }
            }
        }
    }
}
