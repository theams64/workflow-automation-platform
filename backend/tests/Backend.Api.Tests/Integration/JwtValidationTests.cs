using Backend.Api.Data;
using Backend.Api.Models.Entities;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;

namespace Backend.Api.Tests.Integration
{
    public sealed class JwtValidationTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly CustomWebApplicationFactory _factory;
        private readonly HttpClient _client;

        public JwtValidationTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        [Fact]
        public async Task Me_ShouldReturnUnauthorized_WhenAccessTokenIsExpired()
        {
            await _factory.ResetDatabaseAsync();

            var user = await CreateRegisteredUserAsync();

            var token = CreateJwt(user.Id, issuerOverride: null, audienceOverride: null, signingKeyOverride: null, expiresAtUtc: DateTime.UtcNow.AddMinutes(-10), notBeforeUtc: DateTime.UtcNow.AddMinutes(-20));

            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _client.GetAsync("/auth/me");

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task Me_ShouldReturnUnauthorized_WhenIssuerIsWrong()
        {
            await _factory.ResetDatabaseAsync();

            var user = await CreateRegisteredUserAsync();

            var token = CreateJwt(user.Id, issuerOverride: "https://wrong-issuer.example.test", audienceOverride: null, signingKeyOverride: null, expiresAtUtc: DateTime.UtcNow.AddMinutes(10));

            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _client.GetAsync("/auth/me");

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task Me_ShouldReturnUnauthorized_WhenAudienceIsWrong()
        {
            await _factory.ResetDatabaseAsync();

            var user = await CreateRegisteredUserAsync();

            var token = CreateJwt(user.Id, issuerOverride: null, audienceOverride: "wrong-audience", signingKeyOverride: null, expiresAtUtc: DateTime.UtcNow.AddMinutes(10));

            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _client.GetAsync("/auth/me");

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task Me_ShouldReturnUnauthorized_WhenSignatureKeyIsWrong()
        {
            await _factory.ResetDatabaseAsync();

            var user = await CreateRegisteredUserAsync();

            var wrongKey = Convert.ToBase64String(Encoding.UTF8.GetBytes("this-is-a-different-test-signing-key-with-enough-length"));

            var token = CreateJwt(user.Id, issuerOverride: null, audienceOverride: null, signingKeyOverride: wrongKey, expiresAtUtc: DateTime.UtcNow.AddMinutes(10));

            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _client.GetAsync("/auth/me");

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task Me_ShouldReturnCurrentUser_WhenManuallyCreatedTokenIsValid()
        {
            await _factory.ResetDatabaseAsync();

            var user = await CreateRegisteredUserAsync();

            var token = CreateJwt(user.Id, issuerOverride: null, audienceOverride: null, signingKeyOverride: null, expiresAtUtc: DateTime.UtcNow.AddMinutes(10));

            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await _client.GetAsync("/auth/me");

            response.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        private async Task<ApplicationUser> CreateRegisteredUserAsync()
        {
            _client.DefaultRequestHeaders.Authorization = null;

            var email = $"jwt-user-{Guid.NewGuid():N}@example.com";
            var password = "Password123!";

            var registerResponse = await _client.PostAsJsonAsync("/auth/register", new
            {
                email,
                password,
                displayName = "JWT User"
            });

            registerResponse.StatusCode.Should().Be(HttpStatusCode.Created);

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var user = db.Users.Single(candidate => candidate.Email == email);

            return user;
        }

        private string CreateJwt(int userId, string? issuerOverride, string? audienceOverride, string? signingKeyOverride, DateTime expiresAtUtc, DateTime? notBeforeUtc = null)
        {
            using var scope = _factory.Services.CreateScope();

            var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();

            var issuer = issuerOverride ?? configuration["Jwt:Issuer"] ?? throw new InvalidOperationException("Jwt:Issuer is missing.");

            var audience = audienceOverride ?? configuration["Jwt:Audience"] ?? throw new InvalidOperationException("Jwt:Audience is missing.");

            var signingKey = signingKeyOverride ?? configuration["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key is missing.");

            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));

            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var now = DateTime.UtcNow;

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            var token = new JwtSecurityToken(issuer: issuer, audience: audience, claims: claims, notBefore: notBeforeUtc ?? now.AddMinutes(-1), expires: expiresAtUtc, signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}