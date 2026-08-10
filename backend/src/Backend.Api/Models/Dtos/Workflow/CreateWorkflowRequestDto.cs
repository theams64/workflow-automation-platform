using Backend.Api.Models.Validation;
using System.ComponentModel.DataAnnotations;

namespace Backend.Api.Models.Dtos.Workflow
{
    public sealed class CreateWorkflowRequestDto
    {
        [Required]
        [MaxLength(WorkflowLimits.NameMaxLength)]
        public string Name { get; set; } = default!;

        [Required]
        public bool? IsEnabled { get; set; }

        [MaxLength(WorkflowLimits.TriggerTypeMaxLength)]
        public string? TriggerType { get; set; }

        [MaxLength(WorkflowLimits.CronExpressionMaxLength)]
        public string? CronExpression { get; set; }

        [Required]
        [MaxLength(WorkflowLimits.TimezoneMaxLength)]
        public string Timezone { get; set; } = "UTC";
    }
}
