using Backend.Api.Models.Entities;

namespace Backend.Api.Services.Auth
{
    public sealed record IssuedRefreshToken(string RawToken, DateTimeOffset ExpiresAtUtc);
    public sealed record RotatedRefreshToken(ApplicationUser User, string RawToken, DateTimeOffset ExpiresAtUtc);
}
