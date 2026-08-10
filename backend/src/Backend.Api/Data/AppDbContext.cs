using Backend.Api.Models.Entities;
using Backend.Api.Models.Entities.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Backend.Api.Data
{
    public sealed class AppDbContext : IdentityDbContext<ApplicationUser, IdentityRole<int>, int>
    {
        private readonly TimeProvider _timeProvider;
        
        public AppDbContext(DbContextOptions<AppDbContext> options, TimeProvider? timeProvider = null) : base(options) 
        {
            _timeProvider = timeProvider ?? TimeProvider.System;
        }

        public DbSet<UserProfile> UserProfile => Set<UserProfile>();
        public DbSet<Workflow> Workflow => Set<Workflow>();
        public DbSet<WorkflowStep> WorkflowStep => Set<WorkflowStep>();
        public DbSet<WorkflowExecution> WorkflowExecution => Set<WorkflowExecution>();
        public DbSet<StepExecution> StepExecution => Set<StepExecution>();
        public DbSet<RefreshToken> RefreshToken => Set<RefreshToken>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
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
            var now = _timeProvider.GetUtcNow();

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
