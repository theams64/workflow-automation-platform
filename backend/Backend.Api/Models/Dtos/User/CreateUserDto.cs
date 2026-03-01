using Microsoft.AspNetCore.Antiforgery;
using System.ComponentModel.DataAnnotations;

namespace Backend.Api.Models.Dtos.User
{
    public sealed class CreateUserDto
    {
        [Required, EmailAddress]
        public string Email { get; set; } = default!;

        [Required, MinLength(8)]
        public string Password { get; set; } = default!;
    }
}
