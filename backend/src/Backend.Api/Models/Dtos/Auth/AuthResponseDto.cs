namespace Backend.Api.Models.Dtos.Auth
{
    public sealed class AuthResponseDto
    {
        public string AccessToken { get; set; } = default!;
        public int ExpiresInSeconds { get; set; }
    }
}
