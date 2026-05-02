using System.ComponentModel.DataAnnotations;

namespace Backend.Api.Models.Dtos.Workflow
{
    public sealed class UpdateWorkflowRequestDto
    {
        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = default!;

        [Required]
        public bool IsEnabled { get; set; } = default!;

        public string? TriggerType { get; set; }

        [MaxLength(100)]
        public string? CronExpression { get; set; }
    }
}
