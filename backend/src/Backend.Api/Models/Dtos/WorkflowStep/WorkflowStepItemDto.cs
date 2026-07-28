using Backend.Api.Models.Validation;
using System.ComponentModel.DataAnnotations;

namespace Backend.Api.Models.Dtos.WorkflowStep
{
    public sealed class WorkflowStepItemDto
    {
        [Required]
        [MaxLength(WorkflowLimits.StepTypeMaxLength)]
        public string StepType { get; set; } = default!;

        [Required]
        [MaxLength(WorkflowLimits.ConfigJsonMaxLength)]
        public string ConfigJson { get; set; } = default!;
    }
}
