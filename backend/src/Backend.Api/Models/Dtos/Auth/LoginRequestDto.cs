using System.ComponentModel.DataAnnotations;

namespace Backend.Api.Models.Dtos.Auth
{
    public sealed class LoginRequestDto
    {
        [Required]
        public string Email { get; set; } = default!;

        [Required]
        public string Password { get; set; } = default!;
    }
}
