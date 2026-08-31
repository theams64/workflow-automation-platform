using Backend.Api.Models.Validation;
using System.ComponentModel.DataAnnotations;

namespace Backend.Api.Models.Dtos.ManagedConnection
{
    public sealed class UpdateManagedConnectionRequestDto
    {
        [Required]
        [MaxLength(WorkflowLimits.ConnectionNameMaxLength)]
        public string Name { get; set; } = default!;

        [Required]
        public bool? IsEnabled { get; set; }
    }
}