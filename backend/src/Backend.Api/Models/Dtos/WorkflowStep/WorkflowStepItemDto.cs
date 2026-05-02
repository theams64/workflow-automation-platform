using System.ComponentModel.DataAnnotations;

namespace Backend.Api.Models.Dtos.WorkflowStep
{
    public sealed class WorkflowStepItemDto
    {
        [Required]
        [MaxLength(100)]
        public string StepType { get; set; } = default!;

        [Required]
        public string ConfigJson { get; set; } = default!;
    }
}
