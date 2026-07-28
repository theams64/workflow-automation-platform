using Backend.Api.Models.Validation;
using System.ComponentModel.DataAnnotations;

namespace Backend.Api.Models.Dtos.WorkflowStep
{
    public sealed class SaveWorkflowStepsRequestDto
    {
        [Required]
        [MaxLength(WorkflowLimits.MaxStepsPerWorkflow)]
        public List<WorkflowStepItemDto> Steps { get; set; } = new();
    }
}
