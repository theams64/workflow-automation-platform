using Backend.Api.Models.Validation;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend.Api.Models.Entities
{
    [Table("step_execution")]
    public sealed class StepExecution
    {
        [Key]
        [Column("id")]
        public Guid ID { get; set; }

        [Column("workflow_execution_id")]
        public required Guid WorkflowExecutionID { get; set; }

        public WorkflowExecution? WorkflowExecution { get; set; }

        [Column("workflow_step_id")]
        public int? WorkflowStepID { get; set; }

        public WorkflowStep? WorkflowStep { get; set; }

        [Column("step_key")]
        [MaxLength(WorkflowLimits.StepKeyMaxLength)]
        public required string StepKey { get; set; }

        [Column("step_type")]
        [MaxLength(WorkflowLimits.StepTypeMaxLength)]
        public required string StepType { get; set; }

        [Column("step_order")]
        public required int StepOrder { get; set; }

        [Column("status")]
        [MaxLength(WorkflowLimits.ExecutionStatusMaxLength)]
        public required string Status { get; set; }

        [Column("created_at")]
        public required DateTimeOffset CreatedAt { get; set; }

        [Column("started_at")]
        public DateTimeOffset? StartedAt { get; set; }

        [Column("completed_at")]
        public DateTimeOffset? CompletedAt { get; set; }

        [Column("output_json")]
        public string? OutputJson { get; set; }

        [Column("error_code")]
        [MaxLength(WorkflowLimits.ExecutionErrorCodeMaxLength)]
        public string? ErrorCode { get; set; }

        [Column("error_message")]
        [MaxLength(WorkflowLimits.ExecutionErrorMessageMaxLength)]
        public string? ErrorMessage { get; set; }
    }
}
