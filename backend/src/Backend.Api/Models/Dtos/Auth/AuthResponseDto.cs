namespace Backend.Api.Models.Dtos.Auth
{
    public sealed class AuthResponseDto
    {
        public string AccessToken { get; init; } = string.Empty;
        public int ExpiresInSeconds { get; init; }
        public string RefreshToken { get; init; } = string.Empty;
        public DateTimeOffset RefreshTokenExpiresAtUtc { get; init; }
    }
}
