using Backend.Api.Models.Entities;
using Backend.Api.Models.Validation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Api.Data.Configurations
{
    public sealed class WorkflowConfiguration : IEntityTypeConfiguration<Workflow>
    {
        public void Configure(EntityTypeBuilder<Workflow> builder)
        {
            builder.ToTable("workflow");
            builder.HasKey(w => w.ID);

            builder.Property(w => w.UserID).IsRequired();
            builder.Property(w => w.Name).IsRequired().HasMaxLength(WorkflowLimits.NameMaxLength);
            builder.Property(w => w.TriggerType).HasMaxLength(WorkflowLimits.TriggerTypeMaxLength);
            builder.Property(w => w.CronExpression).HasMaxLength(WorkflowLimits.CronExpressionMaxLength);
            builder.Property(w => w.Timezone).IsRequired().HasMaxLength(WorkflowLimits.TimezoneMaxLength);
            builder.Property(w => w.IsEnabled).IsRequired();
            builder.Property(w => w.CreatedAt).IsRequired();
            builder.Property(w => w.UpdatedAt).IsRequired();

            builder.HasIndex(w => new { w.UserID, w.Name }).IsUnique();

            builder.HasOne(w => w.User)
                .WithMany()
                .HasForeignKey(w => w.UserID)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(w => w.WorkflowSteps)
                .WithOne(s => s.Workflow)
                .HasForeignKey(s => s.WorkflowID)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(w => w.WorkflowExecutions)
                .WithOne(e => e.Workflow)
                .HasForeignKey(e => e.WorkflowID)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
