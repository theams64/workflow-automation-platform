using Backend.Api.Models.Entities.Common;
using Backend.Api.Models.Validation;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend.Api.Models.Entities
{
    [Table("workflow_step")]
    public class WorkflowStep : IAuditable
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("id")]
        public int ID { get; set; }

        [Column("workflow_id")]
        public required int WorkflowID { get; set; }

        public Workflow? Workflow { get; set; }

        [Column("step_key")]
        [MaxLength(WorkflowLimits.StepKeyMaxLength)]
        public required string StepKey { get; set; }

        [Column("step_type")]
        [MaxLength(WorkflowLimits.StepTypeMaxLength)]
        public required string StepType { get; set; }

        [Column("config_json")]
        public required string ConfigJson { get; set; }

        [Column("step_order")]
        public required int StepOrder { get; set; }

        public ICollection<StepExecution> StepExecutions { get; set; } = [];

        [Column("created_at")]
        public DateTimeOffset CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTimeOffset UpdatedAt { get; set; }
    }
}
