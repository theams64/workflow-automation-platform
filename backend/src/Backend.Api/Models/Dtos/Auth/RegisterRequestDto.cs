namespace Backend.Api.Models.Dtos.Auth
{
    public sealed class RegisterRequestDto
    {
        public string Email { get; set; } = default!;
        public string Password { get; set; } = default!;
        public string? DisplayName { get; set; }
    }
}
