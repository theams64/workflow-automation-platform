using Backend.Api.Data;
using Backend.Api.Models.Dtos.Auth;
using Backend.Api.Models.Entities;
using Backend.Api.Services.Auth;
using Backend.Api.Tests.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Moq;
using System.Security.Claims;
using Xunit;

namespace Backend.Api.Tests.Services
{
    public class AuthServiceTests
    {
        [Fact]
        public async Task RegisterAsync_ShouldFail_WhenEmailAlreadyExists()
        {
            // Arrange
            using var db = TestHelpers.CreateUnusedDbContext();
            var userManager = TestHelpers.CreateMockUserManager();
            var jwtService = new Mock<IJwtTokenService>();

            userManager
                .Setup(x => x.FindByEmailAsync(It.IsAny<string>()))
                .ReturnsAsync(new ApplicationUser
                {
                    Id = 1,
                    Email = "existing@example.com",
                    UserName = "existing@example.com"
                });

            var service = new AuthService(db, userManager.Object, jwtService.Object);

            var dto = new RegisterRequestDto
            {
                Email = "existing@example.com",
                Password = "Password123!",
                DisplayName = "Existing User"
            };

            // Act
            var result = await service.RegisterAsync(dto);

            // Assert
            result.Succeeded.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.Code == "email_taken");
        }

        [Fact]
        public async Task LoginAsync_ShouldFail_WhenPasswordIsInvalid()
        {
            // Arrange
            using var db = TestHelpers.CreateUnusedDbContext();
            var userManager = TestHelpers.CreateMockUserManager();
            var jwtService = new Mock<IJwtTokenService>();

            var user = new ApplicationUser
            {
                Id = 1,
                Email = "test@example.com",
                UserName = "test@example.com"
            };

            userManager
                .Setup(x => x.FindByEmailAsync(It.IsAny<string>()))
                .ReturnsAsync(user);

            userManager
                .Setup(x => x.CheckPasswordAsync(user, It.IsAny<string>()))
                .ReturnsAsync(false);

            var service = new AuthService(db, userManager.Object, jwtService.Object);

            var dto = new LoginRequestDto
            {
                Email = "test@example.com",
                Password = "WrongPassword123!"
            };

            // Act
            var result = await service.LoginAsync(dto);

            // Assert
            result.Succeeded.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.Code == "invalid_credentials");
        }

        [Fact]
        public async Task LoginAsync_ShouldFail_WhenUserDoesNotExist()
        {
            // Arrange
            using var db = TestHelpers.CreateUnusedDbContext();
            var userManager = TestHelpers.CreateMockUserManager();
            var jwtService = new Mock<IJwtTokenService>();

            userManager
                .Setup(x => x.FindByEmailAsync(It.IsAny<string>()))
                .ReturnsAsync((ApplicationUser?)null);

            var service = new AuthService(db, userManager.Object, jwtService.Object);

            var dto = new LoginRequestDto
            {
                Email = "missing@example.com",
                Password = "Password123"
            };

            // Act
            var result = await service.LoginAsync(dto);

            // Assert
            result.Succeeded.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.Code == "invalid_credentials");
        }

        [Fact]
        public async Task LoginAsync_ShouldReturnToken_WhenCredentialsAreValid()
        {
            // Arrange
            using var db = TestHelpers.CreateUnusedDbContext();
            var userManager = TestHelpers.CreateMockUserManager();
            var jwtService = new Mock<IJwtTokenService>();

            var user = new ApplicationUser
            {
                Id = 1,
                Email = "test@example.com",
                UserName = "test@example.com"
            };

            userManager
                .Setup(x => x.FindByEmailAsync(It.IsAny<string>()))
                .ReturnsAsync(user);

            userManager
                .Setup(x => x.CheckPasswordAsync(user, It.IsAny<string>()))
                .ReturnsAsync(true);

            jwtService
                .Setup(x => x.CreateAccessToken(user))
                .Returns("fake-jwt-token");

            jwtService
                .SetupGet(x => x.AccessTokenLifetimeSeconds)
                .Returns(1800);

            var service = new AuthService(db, userManager.Object, jwtService.Object);

            var dto = new LoginRequestDto
            {
                Email = "test@example.com",
                Password = "Password123"
            };

            // Act
            var result = await service.LoginAsync(dto);

            // Assert
            result.Succeeded.Should().BeTrue();
            result.Data.Should().NotBeNull();
            result.Data!.AccessToken.Should().Be("fake-jwt-token");
            result.Data.ExpiresInSeconds.Should().Be(1800);
        }

        [Fact]
        public async Task GetMeAsync_ShouldFail_WhenNameIdentifierClaimIsMissing()
        {
            // Arrange
            using var db = TestHelpers.CreateUnusedDbContext();
            var userManager = TestHelpers.CreateMockUserManager();
            var jwtService = new Mock<IJwtTokenService>();

            var service = new AuthService(db, userManager.Object, jwtService.Object);

            var principal = new ClaimsPrincipal(new ClaimsIdentity());

            // Act
            var result = await service.GetMeAsync(principal);

            // Assert
            result.Succeeded.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.Code == "unauthorized");
        }
    }
}
