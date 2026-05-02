using System.ComponentModel.DataAnnotations;

namespace Backend.Api.Models.Dtos.WorkflowStep
{
    public sealed class SaveWorkflowStepsRequestDto
    {
        public List<WorkflowStepItemDto> Steps { get; set; } = new();
    }
}
