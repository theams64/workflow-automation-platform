namespace Backend.Api.Models.Dtos.Workflow
{
    public class WorkflowResponseDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = default!;
        public bool IsEnabled { get; set; } = default!;
        public string? TriggerType { get; set; }
        public string? CronExpression { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }
}
