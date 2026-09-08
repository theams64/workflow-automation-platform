using Backend.Api.Models.Dtos.Auth;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;

namespace Backend.Api.Tests.Integration
{
    public sealed class AuthRateLimitingTests : IClassFixture<RateLimitedWebApplicationFactory>
    {
        private readonly RateLimitedWebApplicationFactory _factory;

        public AuthRateLimitingTests(RateLimitedWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task Login_ShouldReturnTooManyRequests_AfterLimitIsExceeded()
        {
            await using var factory = new RateLimitedWebApplicationFactory();

            await factory.ResetDatabaseAsync();

            using var client = factory.CreateClient();

            for (var i = 0; i < 2; i++)
            {
                var response = await client.PostAsJsonAsync("/auth/login", new
                {
                    email = $"missing-{i}@example.com",
                    password = "Password123!"
                });

                response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
            }

            var limitedResponse = await client.PostAsJsonAsync("/auth/login", new
            {
                email = "missing-final@example.com",
                password = "Password123!"
            });

            limitedResponse.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        }

        [Fact]
        public async Task Register_ShouldReturnTooManyRequests_AfterLimitIsExceeded()
        {
            await using var factory = new RateLimitedWebApplicationFactory();

            await factory.ResetDatabaseAsync();

            using var client = factory.CreateClient();

            for (var i = 0; i < 2; i++)
            {
                var response = await client.PostAsJsonAsync("/auth/register", new
                {
                    email = $"limited-register-{Guid.NewGuid():N}@example.com",
                    password = "Password123!",
                    displayName = "Rate Limited Register User"
                });

                response.StatusCode.Should().Be(HttpStatusCode.Created);
            }

            var limitedResponse = await client.PostAsJsonAsync("/auth/register", new
            {
                email = $"limited-register-{Guid.NewGuid():N}@example.com",
                password = "Password123!",
                displayName = "Rate Limited Register User"
            });

            limitedResponse.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        }

        [Fact]
        public async Task Refresh_ShouldReturnTooManyRequests_AfterLimitIsExceeded()
        {
            await using var factory = new RateLimitedWebApplicationFactory();

            await factory.ResetDatabaseAsync();

            using var setupClient = factory.CreateClient();

            var email = $"refresh-rate-{Guid.NewGuid():N}@example.com";
            var password = "Password123!";

            var registerResponse = await setupClient.PostAsJsonAsync("/auth/register", new
            {
                email,
                password,
                displayName = "Refresh Rate User"
            });

            registerResponse.StatusCode.Should().Be(HttpStatusCode.Created);

            var loginResponse = await setupClient.PostAsJsonAsync("/auth/login", new
            {
                email,
                password
            });

            loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var loginBody = await loginResponse.Content.ReadFromJsonAsync<AuthResponseDto>();

            loginBody.Should().NotBeNull();

            using var limitedClient = factory.CreateClient();

            var firstRefresh = await limitedClient.PostAsJsonAsync("/auth/refresh", new
            {
                refreshToken = loginBody!.RefreshToken
            });

            firstRefresh.StatusCode.Should().Be(HttpStatusCode.OK);

            var firstRefreshBody = await firstRefresh.Content.ReadFromJsonAsync<AuthResponseDto>();

            firstRefreshBody.Should().NotBeNull();

            var secondRefresh = await limitedClient.PostAsJsonAsync("/auth/refresh", new
            {
                refreshToken = firstRefreshBody!.RefreshToken
            });

            secondRefresh.StatusCode.Should().Be(HttpStatusCode.OK);

            var secondRefreshBody = await secondRefresh.Content.ReadFromJsonAsync<AuthResponseDto>();

            secondRefreshBody.Should().NotBeNull();

            var limitedResponse = await limitedClient.PostAsJsonAsync("/auth/refresh", new
            {
                refreshToken = secondRefreshBody!.RefreshToken
            });

            limitedResponse.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        }
    }
}