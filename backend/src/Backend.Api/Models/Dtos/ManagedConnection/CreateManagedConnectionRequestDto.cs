using Backend.Api.Models.Validation;
using System.ComponentModel.DataAnnotations;

namespace Backend.Api.Models.Dtos.ManagedConnection
{
    public sealed class CreateManagedConnectionRequestDto
    {
        [Required]
        [MaxLength(WorkflowLimits.ConnectionNameMaxLength)]
        public string Name { get; set; } = default!;

        [Required]
        [MaxLength(WorkflowLimits.ConnectionTypeMaxLength)]
        public string ConnectionType { get; set; } = default!;

        [Required]
        [MaxLength(WorkflowLimits.ConnectionSecretReferenceMaxLength)]
        public string SecretReference { get; set; } = default!;
    }
}
