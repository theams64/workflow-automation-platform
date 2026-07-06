using Backend.Api.Data;
using Backend.Api.Models.Dtos.Auth;
using Backend.Api.Models.Entities;
using Backend.Api.Services.Auth;
using Backend.Api.Tests.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System.Security.Claims;

namespace Backend.Api.Tests.Integration
{
    public sealed class AuthServiceIntegrationTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly CustomWebApplicationFactory _factory;

        public AuthServiceIntegrationTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task RegisterAsync_ShouldFail_WhenIdentityCreationFails()
        {
            // Arrange
            await _factory.ResetDatabaseAsync();

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var userManager = TestHelpers.CreateMockUserManager();
            var signInManager = TestHelpers.CreateMockSignInManager(userManager.Object);
            var jwtService = new Mock<IJwtTokenService>();

            userManager
                .Setup(x => x.FindByEmailAsync(It.IsAny<string>()))
                .ReturnsAsync((ApplicationUser?)null);

            userManager
                .Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
                .ReturnsAsync(IdentityResult.Failed(
                    new IdentityError
                    {
                        Code = "PasswordTooWeak",
                        Description = "Password is too weak."
                    }));

            var service = new AuthService(db, userManager.Object, signInManager.Object, jwtService.Object);

            var dto = new RegisterRequestDto
            {
                Email = "new@example.com",
                Password = "Password123!",
                DisplayName = "New User"
            };

            // Act
            var result = await service.RegisterAsync(dto);

            // Assert
            result.Succeeded.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.Code == "PasswordTooWeak");
            db.UserProfile.Should().BeEmpty();
        }

        [Fact]
        public async Task GetMeAsync_ShouldFail_WhenProfileDoesNotExist()
        {
            // Arrange
            await _factory.ResetDatabaseAsync();

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var userManager = TestHelpers.CreateMockUserManager();
            var signInManager = TestHelpers.CreateMockSignInManager(userManager.Object);
            var jwtService = new Mock<IJwtTokenService>();

            var user = new ApplicationUser
            {
                Id = 42,
                Email = "me@example.com",
                UserName = "me@example.com"
            };

            userManager
                .Setup(x => x.FindByIdAsync("42"))
                .ReturnsAsync(user);

            var service = new AuthService(db, userManager.Object, signInManager.Object, jwtService.Object);

            var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "42")
            ]));

            // Act
            var result = await service.GetMeAsync(principal);

            // Assert
            result.Succeeded.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.Code == "profile_missing");
        }

        [Fact]
        public async Task GetMeAsync_ShouldReturnProfile_WhenUserAndProfileExist()
        {
            // Arrange
            await _factory.ResetDatabaseAsync();

            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var createdAt = DateTimeOffset.UtcNow.AddDays(-1);
            var updatedAt = DateTimeOffset.UtcNow;

            // Insert matching Identity user first
            db.Users.Add(new ApplicationUser
            {
                Id = 7,
                Email = "user@example.com",
                UserName = "user@example.com",
                NormalizedEmail = "USER@EXAMPLE.COM",
                NormalizedUserName = "USER@EXAMPLE.COM"
            });

            await db.SaveChangesAsync();

            // Then insert profile row
            db.UserProfile.Add(new UserProfile
            {
                IdentityUserId = 7,
                DisplayName = "Test User",
                CreatedAt = createdAt,
                UpdatedAt = updatedAt
            });

            await db.SaveChangesAsync();

            var userManager = TestHelpers.CreateMockUserManager();
            var signInManager = TestHelpers.CreateMockSignInManager(userManager.Object);
            var jwtService = new Mock<IJwtTokenService>();

            userManager
                .Setup(x => x.FindByIdAsync("7"))
                .ReturnsAsync(new ApplicationUser
                {
                    Id = 7,
                    Email = "user@example.com",
                    UserName = "user@example.com"
                });

            var service = new AuthService(db, userManager.Object, signInManager.Object, jwtService.Object);

            var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "7")
            ]));

            // Act
            var result = await service.GetMeAsync(principal);

            // Assert
            result.Succeeded.Should().BeTrue();
            result.Data.Should().NotBeNull();
            result.Data!.IdentityUserId.Should().Be(7);
            result.Data.Email.Should().Be("user@example.com");
            result.Data.DisplayName.Should().Be("Test User");
        }
    }
}
