using Backend.Api.Configuration;
using Backend.Api.Models.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Backend.Api.Services.Auth
{
    public sealed class JwtTokenService : IJwtTokenService
    {
        private readonly JwtOptions _options;
        private readonly SigningCredentials _signingCredentials;

        public JwtTokenService(IOptions<JwtOptions> options)
        {
            _options = options.Value;

            var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key));

            _signingCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        }

        public int AccessTokenLifetimeSeconds => checked(_options.AccessTokenMinutes * 60);

        public string CreateAccessToken(ApplicationUser user)
        {
            ArgumentNullException.ThrowIfNull(user);

            var now = DateTime.UtcNow;

            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new(ClaimTypes.Email, user.Email ?? string.Empty),
                new(ClaimTypes.Name, user.UserName ?? user.Email ?? string.Empty)
            };

            var token = new JwtSecurityToken(
                issuer: _options.Issuer,
                audience: _options.Audience,
                claims: claims,
                notBefore: now,
                expires: now.AddMinutes(_options.AccessTokenMinutes),
                signingCredentials: _signingCredentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
