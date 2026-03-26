using Backend.Api.Models.Dtos.Auth;
using Backend.Api.Models.Dtos.User;
using FluentAssertions;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Xunit;

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
    }
}
