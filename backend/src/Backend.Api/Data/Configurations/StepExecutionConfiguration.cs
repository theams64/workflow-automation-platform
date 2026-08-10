using Backend.Api.Models.Entities;
using Backend.Api.Models.Validation;
using Backend.Api.WorkflowEngine.Execution;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Api.Data.Configurations
{
    public sealed class StepExecutionConfiguration : IEntityTypeConfiguration<StepExecution>
    {
        public void Configure(EntityTypeBuilder<StepExecution> builder)
        {
            builder.ToTable("step_execution", table =>
            {
                table.HasCheckConstraint(
                    "CK_step_execution_status",
                    $"status IN ('{ExecutionStatuses.Pending}','{ExecutionStatuses.Running}','{ExecutionStatuses.Succeeded}','{ExecutionStatuses.Failed}','{ExecutionStatuses.Cancelled}')"
                );
            });
            builder.HasKey(x => x.ID);

            builder.Property(x => x.ID).ValueGeneratedNever();
            builder.Property(x => x.WorkflowExecutionID).IsRequired();
            builder.Property(x => x.StepKey).IsRequired().HasMaxLength(WorkflowLimits.StepKeyMaxLength);
            builder.Property(x => x.StepType).IsRequired().HasMaxLength(WorkflowLimits.StepTypeMaxLength);
            builder.Property(x => x.StepOrder).IsRequired();
            builder.Property(x => x.Status).IsRequired().HasMaxLength(WorkflowLimits.ExecutionStatusMaxLength);
            builder.Property(x => x.CreatedAt).IsRequired();
            builder.Property(x => x.OutputJson).HasColumnType("jsonb");
            builder.Property(x => x.ErrorCode).HasMaxLength(WorkflowLimits.ExecutionErrorCodeMaxLength);
            builder.Property(x => x.ErrorMessage).HasMaxLength(WorkflowLimits.ExecutionErrorMessageMaxLength);

            builder.HasIndex(x => new { x.WorkflowExecutionID, x.StepOrder }).IsUnique();
            builder.HasIndex(x => new { x.WorkflowExecutionID, x.StepKey }).IsUnique();

            builder.HasOne(x => x.WorkflowExecution)
                .WithMany(x => x.StepExecutions)
                .HasForeignKey(x => x.WorkflowExecutionID)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.WorkflowStep)
                .WithMany(x => x.StepExecutions)
                .HasForeignKey(x => x.WorkflowStepID)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }
}
