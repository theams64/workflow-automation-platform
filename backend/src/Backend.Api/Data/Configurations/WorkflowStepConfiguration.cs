using Backend.Api.Models.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Api.Data.Configurations
{
    public sealed class WorkflowStepConfiguration : IEntityTypeConfiguration<WorkflowStep>
    {
        public void Configure(EntityTypeBuilder<WorkflowStep> builder)
        {
            builder.ToTable("workflow_step");

            builder.HasKey(s => s.ID);

            builder.Property(s => s.StepType)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(s => s.ConfigJson)
                .IsRequired()
                .HasColumnType("jsonb");

            builder.Property(s => s.StepOrder)
                .IsRequired();

            builder.Property(s => s.CreatedAt)
                .IsRequired();

            builder.Property(s => s.UpdatedAt)
                .IsRequired();

            builder.HasIndex(s => new { s.WorkflowID, s.StepOrder })
                .IsUnique();
        }
    }
}
