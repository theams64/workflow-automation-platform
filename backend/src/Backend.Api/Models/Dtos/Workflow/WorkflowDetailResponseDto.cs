using Backend.Api.Models.Dtos.WorkflowStep;

namespace Backend.Api.Models.Dtos.Workflow
{
    public sealed class WorkflowDetailResponseDto : WorkflowResponseDto
    {
        public List<WorkflowStepResponseDto> Steps { get; set; } = new();
    }
}
