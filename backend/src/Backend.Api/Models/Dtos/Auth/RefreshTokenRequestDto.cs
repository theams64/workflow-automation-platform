using System.ComponentModel.DataAnnotations;

namespace Backend.Api.Models.Dtos.Auth
{
    public sealed class RefreshTokenRequestDto
    {
        [Required]
        [StringLength(maximumLength: 256, MinimumLength = 40)]
        public string RefreshToken { get; init; } = string.Empty;
    }
}
