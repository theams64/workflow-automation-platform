using Backend.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Api.Data.Configurations
{
    public sealed class WorkflowConfiguration
    {
        public void Configure(EntityTypeBuilder<Workflow> builder)
        {
            builder.ToTable("workflow");

            builder.HasKey(w => w.ID);

            builder.Property(w => w.UserID)
                .IsRequired();

            builder.Property(w => w.Name)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(w => w.CronExpression)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(w => w.IsEnabled)
                .IsRequired();

            builder.Property(w => w.CreatedAt)
                .IsRequired();

            builder.Property(w => w.UpdatedAt)
                .IsRequired();

            builder.HasIndex(w => new { w.UserID, w.Name })
                .IsUnique();

            builder.HasMany(w => w.WorkflowSteps)
                .WithOne(s => s.Workflow)
                .HasForeignKey(s => s.WorkflowID)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
