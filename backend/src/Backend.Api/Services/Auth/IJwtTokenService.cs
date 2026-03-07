using Backend.Api.Models.Entities;

namespace Backend.Api.Services.Auth
{
    public interface IJwtTokenService
    {
        string CreateAccessToken(ApplicationUser user);
        int AccessTokenLifetimeSeconds { get; }
    }
}
