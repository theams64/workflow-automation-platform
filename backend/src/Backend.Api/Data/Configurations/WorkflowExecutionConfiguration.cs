using Backend.Api.Models.Entities;
using Backend.Api.Models.Validation;
using Backend.Api.WorkflowEngine.Execution;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Api.Data.Configurations
{
    public sealed class WorkflowExecutionConfiguration : IEntityTypeConfiguration<WorkflowExecution>
    {
        public void Configure(EntityTypeBuilder<WorkflowExecution> builder)
        {
            builder.ToTable("workflow_execution", table =>
            {
                table.HasCheckConstraint(
                    "CK_workflow_execution_status",
                    $"status IN ('{ExecutionStatuses.Pending}','{ExecutionStatuses.Running}','{ExecutionStatuses.Succeeded}','{ExecutionStatuses.Failed}','{ExecutionStatuses.Cancelled}')"
                );
            });
            builder.HasKey(x => x.ID);

            builder.Property(x => x.ID).ValueGeneratedNever();
            builder.Property(x => x.WorkflowID).IsRequired();
            builder.Property(x => x.InitiatingUserID).IsRequired();
            builder.Property(x => x.TriggerType).IsRequired().HasMaxLength(WorkflowLimits.TriggerTypeMaxLength);
            builder.Property(x => x.Status).IsRequired().HasMaxLength(WorkflowLimits.ExecutionStatusMaxLength);
            builder.Property(x => x.CreatedAt).IsRequired();
            builder.Property(x => x.EffectiveDate).IsRequired().HasColumnType("date");
            builder.Property(x => x.Timezone).IsRequired().HasMaxLength(WorkflowLimits.TimezoneMaxLength);
            builder.Property(x => x.InputJson).IsRequired().HasColumnType("jsonb");
            builder.Property(x => x.ErrorCode).HasMaxLength(WorkflowLimits.ExecutionErrorCodeMaxLength);
            builder.Property(x => x.ErrorMessage).HasMaxLength(WorkflowLimits.ExecutionErrorMessageMaxLength);

            builder.HasIndex(x => new { x.WorkflowID, x.CreatedAt });
            builder.HasIndex(x => new { x.InitiatingUserID, x.CreatedAt });
            builder.HasIndex(x => x.Status);

            builder.HasOne(x => x.InitiatingUser)
                .WithMany()
                .HasForeignKey(x => x.InitiatingUserID)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
