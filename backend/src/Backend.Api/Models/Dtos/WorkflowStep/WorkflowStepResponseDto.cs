namespace Backend.Api.Models.Dtos.WorkflowStep
{
    public class WorkflowStepResponseDto
    {
        public int Id { get; set; }
        public int WorkflowId { get; set; }
        public string StepKey { get; set; } = default!;
        public string StepType { get; set; } = default!;
        public string ConfigJson { get; set; } = default!;
        public int StepOrder { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }
}
