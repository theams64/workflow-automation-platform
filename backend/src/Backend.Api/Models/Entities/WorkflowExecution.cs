using Backend.Api.Models.Validation;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend.Api.Models.Entities
{
    [Table("workflow_execution")]
    public sealed class WorkflowExecution
    {
        [Key]
        [Column("id")]
        public Guid ID { get; set; }

        [Column("workflow_id")]
        public required int WorkflowID { get; set; }

        public Workflow? Workflow { get; set; }

        [Column("initiating_user_id")]
        public required int InitiatingUserID { get; set; }

        public ApplicationUser? InitiatingUser { get; set; }

        [Column("trigger_type")]
        [MaxLength(WorkflowLimits.TriggerTypeMaxLength)]
        public required string TriggerType { get; set; }

        [Column("status")]
        [MaxLength(WorkflowLimits.ExecutionStatusMaxLength)]
        public required string Status { get; set; }

        [Column("created_at")]
        public required DateTimeOffset CreatedAt { get; set; }

        [Column("started_at")]
        public DateTimeOffset? StartedAt { get; set; }

        [Column("completed_at")]
        public DateTimeOffset? CompletedAt { get; set; }

        [Column("scheduled_for")]
        public DateTimeOffset? ScheduledFor { get; set; }

        [Column("effective_date")]
        public required DateOnly EffectiveDate { get; set; }

        [Column("timezone")]
        [MaxLength(WorkflowLimits.TimezoneMaxLength)]
        public required string Timezone { get; set; }

        [Column("input_json")]
        public required string InputJson { get; set; }

        [Column("error_code")]
        [MaxLength(WorkflowLimits.ExecutionErrorCodeMaxLength)]
        public string? ErrorCode { get; set; }

        [Column("error_message")]
        [MaxLength(WorkflowLimits.ExecutionErrorMessageMaxLength)]
        public string? ErrorMessage { get; set; }

        public ICollection<StepExecution> StepExecutions { get; set; } = [];
    }
}
