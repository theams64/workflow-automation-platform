using Backend.Api.Data;
using Backend.Api.Models.Dtos.Auth;
using Backend.Api.Models.Dtos.User;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;

namespace Backend.Api.Tests.Integration
{
    public sealed class AuthEndpointsTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly CustomWebApplicationFactory _factory;
        private readonly HttpClient _client;

        public AuthEndpointsTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
            _client = factory.CreateClient();
        }

        // Register
        [Fact]
        public async Task Register_ShouldReturnCreated()
        {
            await _factory.ResetDatabaseAsync();
            _client.DefaultRequestHeaders.Authorization = null;

            var request = new
            {
                email = $"user-{Guid.NewGuid():N}@example.com",
                password = "Password123!",
                displayName = "New User"
            };

            var response = await _client.PostAsJsonAsync("/auth/register", request);

            response.StatusCode.Should().Be(HttpStatusCode.Created);
        }

        [Fact]
        public async Task Register_ShouldReturnBadRequest_WhenEmailAlreadyExists()
        {
            await _factory.ResetDatabaseAsync();
            _client.DefaultRequestHeaders.Authorization = null;

            var email = $"user-{Guid.NewGuid():N}@example.com";
            var password = "Password123!";

            var firstResponse = await _client.PostAsJsonAsync("/auth/register", new
            {
                email,
                password,
                displayName = "First User"
            });

            firstResponse.StatusCode.Should().Be(HttpStatusCode.Created);

            var secondResponse = await _client.PostAsJsonAsync("/auth/register", new
            {
                email,
                password,
                displayName = "Second User"
            });

            secondResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Register_ShouldReturnBadRequest_WhenEmailIsInvalid()
        {
            await _factory.ResetDatabaseAsync();
            _client.DefaultRequestHeaders.Authorization = null;

            var response = await _client.PostAsJsonAsync("/auth/register", new
            {
                email = "not-an-email",
                password = "Password123!",
                displayName = "Invalid Email User"
            });

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Register_ShouldReturnBadRequest_WhenEmailIsTooLong()
        {
            await _factory.ResetDatabaseAsync();
            _client.DefaultRequestHeaders.Authorization = null;

            var longLocalPart = new string('a', 250);

            var response = await _client.PostAsJsonAsync("/auth/register", new
            {
                email = $"{longLocalPart}@example.com",
                password = "Password123!",
                displayName = "Long Email User"
            });

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Register_ShouldReturnBadRequest_WhenPasswordIsTooLong()
        {
            await _factory.ResetDatabaseAsync();
            _client.DefaultRequestHeaders.Authorization = null;

            var response = await _client.PostAsJsonAsync("/auth/register", new
            {
                email = $"user-{Guid.NewGuid():N}@example.com",
                password = new string('A', 129) + "1!",
                displayName = "Long Password User"
            });

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Register_ShouldReturnBadRequest_WhenDisplayNameIsTooLong()
        {
            await _factory.ResetDatabaseAsync();
            _client.DefaultRequestHeaders.Authorization = null;

            var response = await _client.PostAsJsonAsync("/auth/register", new
            {
                email = $"user-{Guid.NewGuid():N}@example.com",
                password = "Password123!",
                displayName = new string('x', 101)
            });

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        // Login
        [Fact]
        public async Task Login_ShouldReturnToken_AfterSuccessfulRegistration()
        {
            await _factory.ResetDatabaseAsync();
            _client.DefaultRequestHeaders.Authorization = null;

            var email = $"user-{Guid.NewGuid():N}@example.com";
            var password = "Password123!";

            var registerResponse = await _client.PostAsJsonAsync("/auth/register", new
            {
                email,
                password,
                displayName = "Login Test User"
            });

            registerResponse.StatusCode.Should().Be(HttpStatusCode.Created);

            var loginResponse = await _client.PostAsJsonAsync("/auth/login", new
            {
                email,
                password
            });

            loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var loginBody = await loginResponse.Content.ReadFromJsonAsync<AuthResponseDto>();

            loginBody.Should().NotBeNull();
            loginBody!.AccessToken.Should().NotBeNullOrWhiteSpace();
            loginBody.ExpiresInSeconds.Should().BeGreaterThan(0);

            loginBody.RefreshToken.Should().NotBeNullOrWhiteSpace();
            loginBody.RefreshTokenExpiresAtUtc.Should().BeAfter(DateTimeOffset.UtcNow);
        }

        [Fact]
        public async Task Login_ShouldStoreRefreshTokenHash_NotRawRefreshToken()
        {
            await _factory.ResetDatabaseAsync();
            _client.DefaultRequestHeaders.Authorization = null;

            var email = $"user-{Guid.NewGuid():N}@example.com";
            var password = "Password123!";

            var registerResponse = await _client.PostAsJsonAsync("/auth/register", new
            {
                email,
                password,
                displayName = "Refresh Hash User"
            });

            registerResponse.StatusCode.Should().Be(HttpStatusCode.Created);

            var loginResponse = await _client.PostAsJsonAsync("/auth/login", new
            {
                email,
                password
            });

            loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var loginBody = await loginResponse.Content.ReadFromJsonAsync<AuthResponseDto>();
            loginBody.Should().NotBeNull();
            loginBody!.RefreshToken.Should().NotBeNullOrWhiteSpace();

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var storedToken = db.RefreshToken.Single();

            storedToken.TokenHash.Should().NotBe(loginBody.RefreshToken);
            storedToken.TokenHash.Should().HaveLength(64);

            var expectedHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(loginBody.RefreshToken)));

            storedToken.TokenHash.Should().Be(expectedHash);
            storedToken.RevokedAt.Should().BeNull();
            storedToken.ExpiresAt.Should().BeAfter(DateTimeOffset.UtcNow);
        }

        [Fact]
        public async Task Login_ShouldReturnUnauthorized_WhenPasswordIsWrong()
        {
            await _factory.ResetDatabaseAsync();
            _client.DefaultRequestHeaders.Authorization = null;

            var email = $"user-{Guid.NewGuid():N}@example.com";
            var password = "Password123!";

            var registerResponse = await _client.PostAsJsonAsync("/auth/register", new
            {
                email,
                password,
                displayName = "Login Test User"
            });

            registerResponse.StatusCode.Should().Be(HttpStatusCode.Created);

            var loginResponse = await _client.PostAsJsonAsync("/auth/login", new
            {
                email,
                password = "WrongPassword123!"
            });

            loginResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task Login_ShouldReturnBadRequest_WhenEmailIsInvalid()
        {
            await _factory.ResetDatabaseAsync();
            _client.DefaultRequestHeaders.Authorization = null;

            var response = await _client.PostAsJsonAsync("/auth/login", new
            {
                email = "not-an-email",
                password = "Password123!"
            });

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Login_ShouldReturnBadRequest_WhenPasswordIsMissing()
        {
            await _factory.ResetDatabaseAsync();
            _client.DefaultRequestHeaders.Authorization = null;

            var response = await _client.PostAsJsonAsync("/auth/login", new
            {
                email = "user@example.com",
                password = ""
            });

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        // Refresh
        [Fact]
        public async Task Refresh_ShouldReturnBadRequest_WhenRefreshTokenIsMissing()
        {
            await _factory.ResetDatabaseAsync();
            _client.DefaultRequestHeaders.Authorization = null;

            var response = await _client.PostAsJsonAsync("/auth/refresh", new
            {
                refreshToken = ""
            });

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Refresh_ShouldReturnBadRequest_WhenRefreshTokenIsTooShort()
        {
            await _factory.ResetDatabaseAsync();
            _client.DefaultRequestHeaders.Authorization = null;

            var response = await _client.PostAsJsonAsync("/auth/refresh", new
            {
                refreshToken = "too-short"
            });

            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task Refresh_ShouldReturnNewTokens_AndRevokeOldRefreshToken()
        {
            await _factory.ResetDatabaseAsync();
            _client.DefaultRequestHeaders.Authorization = null;

            var email = $"user-{Guid.NewGuid():N}@example.com";
            var password = "Password123!";

            var registerResponse = await _client.PostAsJsonAsync("/auth/register", new
            {
                email,
                password,
                displayName = "Refresh User"
            });

            registerResponse.StatusCode.Should().Be(HttpStatusCode.Created);

            var loginResponse = await _client.PostAsJsonAsync("/auth/login", new
            {
                email,
                password
            });

            loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var loginBody = await loginResponse.Content.ReadFromJsonAsync<AuthResponseDto>();
            loginBody.Should().NotBeNull();

            var refreshResponse = await _client.PostAsJsonAsync("/auth/refresh", new
            {
                refreshToken = loginBody!.RefreshToken
            });

            refreshResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var refreshBody = await refreshResponse.Content.ReadFromJsonAsync<AuthResponseDto>();
            refreshBody.Should().NotBeNull();

            refreshBody!.AccessToken.Should().NotBeNullOrWhiteSpace();
            refreshBody.AccessToken.Should().NotBe(loginBody.AccessToken);

            refreshBody.RefreshToken.Should().NotBeNullOrWhiteSpace();
            refreshBody.RefreshToken.Should().NotBe(loginBody.RefreshToken);

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var tokens = db.RefreshToken.ToList();

            tokens.Should().HaveCount(2);
            tokens.Should().ContainSingle(token => token.RevokedAt != null);
            tokens.Should().ContainSingle(token => token.RevokedAt == null);
        }

        [Fact]
        public async Task Refresh_ShouldRejectOldRefreshToken_AndRevokeCurrentFamily_WhenOldTokenIsReused()
        {
            await _factory.ResetDatabaseAsync();
            _client.DefaultRequestHeaders.Authorization = null;

            var email = $"user-{Guid.NewGuid():N}@example.com";
            var password = "Password123!";

            var registerResponse = await _client.PostAsJsonAsync("/auth/register", new
            {
                email,
                password,
                displayName = "Reuse Detection User"
            });

            registerResponse.StatusCode.Should().Be(HttpStatusCode.Created);

            var loginResponse = await _client.PostAsJsonAsync("/auth/login", new
            {
                email,
                password
            });

            loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var loginBody = await loginResponse.Content.ReadFromJsonAsync<AuthResponseDto>();
            loginBody.Should().NotBeNull();

            var firstRefreshResponse = await _client.PostAsJsonAsync("/auth/refresh", new
            {
                refreshToken = loginBody!.RefreshToken
            });

            firstRefreshResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var firstRefreshBody = await firstRefreshResponse.Content.ReadFromJsonAsync<AuthResponseDto>();
            firstRefreshBody.Should().NotBeNull();

            var reusedOldTokenResponse = await _client.PostAsJsonAsync("/auth/refresh", new
            {
                refreshToken = loginBody.RefreshToken
            });

            reusedOldTokenResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

            var currentTokenAfterReuseResponse = await _client.PostAsJsonAsync("/auth/refresh", new
            {
                refreshToken = firstRefreshBody!.RefreshToken
            });

            currentTokenAfterReuseResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var tokens = db.RefreshToken.ToList();

            tokens.Should().HaveCount(2);
            tokens.Should().OnlyContain(token => token.RevokedAt != null);
        }

        // Logout
        [Fact]
        public async Task Logout_ShouldRevokeRefreshTokenFamily()
        {
            await _factory.ResetDatabaseAsync();
            _client.DefaultRequestHeaders.Authorization = null;

            var email = $"user-{Guid.NewGuid():N}@example.com";
            var password = "Password123!";

            var registerResponse = await _client.PostAsJsonAsync("/auth/register", new
            {
                email,
                password,
                displayName = "Logout User"
            });

            registerResponse.StatusCode.Should().Be(HttpStatusCode.Created);

            var loginResponse = await _client.PostAsJsonAsync("/auth/login", new
            {
                email,
                password
            });

            loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var loginBody = await loginResponse.Content.ReadFromJsonAsync<AuthResponseDto>();
            loginBody.Should().NotBeNull();

            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginBody!.AccessToken);

            var logoutResponse = await _client.PostAsJsonAsync("/auth/logout", new
            {
                refreshToken = loginBody.RefreshToken
            });

            logoutResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

            _client.DefaultRequestHeaders.Authorization = null;

            var refreshAfterLogoutResponse = await _client.PostAsJsonAsync("/auth/refresh", new
            {
                refreshToken = loginBody.RefreshToken
            });

            refreshAfterLogoutResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var storedToken = db.RefreshToken.Single();

            storedToken.RevokedAt.Should().NotBeNull();
        }

        [Fact]
        public async Task Logout_ShouldReturnUnauthorized_WhenAccessTokenIsMissing()
        {
            await _factory.ResetDatabaseAsync();
            _client.DefaultRequestHeaders.Authorization = null;

            var response = await _client.PostAsJsonAsync("/auth/logout", new
            {
                refreshToken = "not-a-real-refresh-token-but-long-enough-for-validation"
            });

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task Logout_ShouldNotRevokeAnotherUsersRefreshToken()
        {
            await _factory.ResetDatabaseAsync();
            _client.DefaultRequestHeaders.Authorization = null;

            var userAEmail = $"user-a-{Guid.NewGuid():N}@example.com";
            var userBEmail = $"user-b-{Guid.NewGuid():N}@example.com";
            var password = "Password123!";

            var registerAResponse = await _client.PostAsJsonAsync("/auth/register", new
            {
                email = userAEmail,
                password,
                displayName = "User A"
            });

            registerAResponse.StatusCode.Should().Be(HttpStatusCode.Created);

            var registerBResponse = await _client.PostAsJsonAsync("/auth/register", new
            {
                email = userBEmail,
                password,
                displayName = "User B"
            });

            registerBResponse.StatusCode.Should().Be(HttpStatusCode.Created);

            var loginAResponse = await _client.PostAsJsonAsync("/auth/login", new
            {
                email = userAEmail,
                password
            });

            loginAResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var loginABody = await loginAResponse.Content.ReadFromJsonAsync<AuthResponseDto>();
            loginABody.Should().NotBeNull();

            var loginBResponse = await _client.PostAsJsonAsync("/auth/login", new
            {
                email = userBEmail,
                password
            });

            loginBResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var loginBBody = await loginBResponse.Content.ReadFromJsonAsync<AuthResponseDto>();
            loginBBody.Should().NotBeNull();

            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginBBody!.AccessToken);

            var logoutResponse = await _client.PostAsJsonAsync("/auth/logout", new
            {
                refreshToken = loginABody!.RefreshToken
            });

            logoutResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

            _client.DefaultRequestHeaders.Authorization = null;

            var userARefreshResponse = await _client.PostAsJsonAsync("/auth/refresh", new
            {
                refreshToken = loginABody.RefreshToken
            });

            userARefreshResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        // Me
        [Fact]
        public async Task Me_ShouldReturnUnauthorized_WhenTokenIsMissing()
        {
            await _factory.ResetDatabaseAsync();
            _client.DefaultRequestHeaders.Authorization = null;

            var response = await _client.GetAsync("/auth/me");

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task Me_ShouldReturnUnauthorized_WhenTokenIsInvalid()
        {
            await _factory.ResetDatabaseAsync();
            _client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", "this-is-not-a-valid-token");

            var response = await _client.GetAsync("/auth/me");

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task Me_ShouldReturnCurrentUser_WhenTokenIsValid()
        {
            await _factory.ResetDatabaseAsync();
            _client.DefaultRequestHeaders.Authorization = null;

            var email = $"user-{Guid.NewGuid():N}@example.com";
            var password = "Password123!";
            var displayName = "Authorized User";

            var registerResponse = await _client.PostAsJsonAsync("/auth/register", new
            {
                email,
                password,
                displayName
            });

            registerResponse.StatusCode.Should().Be(HttpStatusCode.Created);

            var loginResponse = await _client.PostAsJsonAsync("/auth/login", new
            {
                email,
                password
            });

            loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var loginBody = await loginResponse.Content.ReadFromJsonAsync<AuthResponseDto>();
            loginBody.Should().NotBeNull();

            _client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", loginBody!.AccessToken);

            var meResponse = await _client.GetAsync("/auth/me");

            meResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var meBody = await meResponse.Content.ReadFromJsonAsync<UserProfileDto>();
            meBody.Should().NotBeNull();
            meBody!.Email.Should().Be(email);
            meBody.DisplayName.Should().Be(displayName);
            meBody.IdentityUserId.Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task Me_ShouldReturnUnauthorized_WhenRefreshTokenIsUsedAsBearerToken()
        {
            await _factory.ResetDatabaseAsync();
            _client.DefaultRequestHeaders.Authorization = null;

            var email = $"user-{Guid.NewGuid():N}@example.com";
            var password = "Password123!";

            var registerResponse = await _client.PostAsJsonAsync("/auth/register", new
            {
                email,
                password,
                displayName = "Refresh Token Bearer User"
            });

            registerResponse.StatusCode.Should().Be(HttpStatusCode.Created);

            var loginResponse = await _client.PostAsJsonAsync("/auth/login", new
            {
                email,
                password
            });

            loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var loginBody = await loginResponse.Content.ReadFromJsonAsync<AuthResponseDto>();
            loginBody.Should().NotBeNull();
            loginBody!.RefreshToken.Should().NotBeNullOrWhiteSpace();

            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginBody.RefreshToken);

            var meResponse = await _client.GetAsync("/auth/me");

            meResponse.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        [Fact]
        public async Task Me_ShouldAcceptNewAccessToken_AfterRefresh()
        {
            await _factory.ResetDatabaseAsync();
            _client.DefaultRequestHeaders.Authorization = null;

            var email = $"user-{Guid.NewGuid():N}@example.com";
            var password = "Password123!";
            var displayName = "Refreshed Access User";

            var registerResponse = await _client.PostAsJsonAsync("/auth/register", new
            {
                email,
                password,
                displayName
            });

            registerResponse.StatusCode.Should().Be(HttpStatusCode.Created);

            var loginResponse = await _client.PostAsJsonAsync("/auth/login", new
            {
                email,
                password
            });

            loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var loginBody = await loginResponse.Content.ReadFromJsonAsync<AuthResponseDto>();
            loginBody.Should().NotBeNull();

            var refreshResponse = await _client.PostAsJsonAsync("/auth/refresh", new
            {
                refreshToken = loginBody!.RefreshToken
            });

            refreshResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var refreshBody = await refreshResponse.Content.ReadFromJsonAsync<AuthResponseDto>();
            refreshBody.Should().NotBeNull();

            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", refreshBody!.AccessToken);

            var meResponse = await _client.GetAsync("/auth/me");

            meResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var meBody = await meResponse.Content.ReadFromJsonAsync<UserProfileDto>();

            meBody.Should().NotBeNull();
            meBody!.Email.Should().Be(email);
            meBody.DisplayName.Should().Be(displayName);
        }

        [Fact]
        public async Task Me_ShouldStillAcceptOldAccessToken_AfterRefresh()
        {
            await _factory.ResetDatabaseAsync();
            _client.DefaultRequestHeaders.Authorization = null;

            var email = $"user-{Guid.NewGuid():N}@example.com";
            var password = "Password123!";
            var displayName = "Old Access Still Valid User";

            var registerResponse = await _client.PostAsJsonAsync("/auth/register", new
            {
                email,
                password,
                displayName
            });

            registerResponse.StatusCode.Should().Be(HttpStatusCode.Created);

            var loginResponse = await _client.PostAsJsonAsync("/auth/login", new
            {
                email,
                password
            });

            loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var loginBody = await loginResponse.Content.ReadFromJsonAsync<AuthResponseDto>();
            loginBody.Should().NotBeNull();

            var refreshResponse = await _client.PostAsJsonAsync("/auth/refresh", new
            {
                refreshToken = loginBody!.RefreshToken
            });

            refreshResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", loginBody.AccessToken);

            var meResponse = await _client.GetAsync("/auth/me");

            meResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var meBody = await meResponse.Content.ReadFromJsonAsync<UserProfileDto>();

            meBody.Should().NotBeNull();
            meBody!.Email.Should().Be(email);
        }
    }
}
