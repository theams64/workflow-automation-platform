using System.ComponentModel.DataAnnotations;

namespace Backend.Api.Models.Dtos.Auth
{
    public sealed class LoginRequestDto
    {
        [Required]
        [EmailAddress]
        [StringLength(254)]
        public string Email { get; init; } = string.Empty;

        [Required]
        [StringLength(maximumLength: 128, MinimumLength = 1)]
        public string Password { get; init; } = string.Empty;
    }
}
