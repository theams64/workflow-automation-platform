using System.ComponentModel.DataAnnotations;

namespace Backend.Api.Models.Dtos.Auth
{
    public sealed class RegisterRequestDto
    {
        [Required]
        [EmailAddress]
        [StringLength(254)]
        public string Email { get; init; } = string.Empty;

        [Required]
        [StringLength(maximumLength: 128, MinimumLength = 8)]
        public string Password { get; init; } = string.Empty;

        [StringLength(100)]
        public string? DisplayName { get; init; }
    }
}
