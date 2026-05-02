namespace Backend.Api.Models.Dtos.WorkflowStep
{
    public sealed class WorkflowStepListResponseDto
    {
        public int WorkflowId { get; set; }
        public List<WorkflowStepResponseDto> Steps { get; set; } = new();
    }
}
