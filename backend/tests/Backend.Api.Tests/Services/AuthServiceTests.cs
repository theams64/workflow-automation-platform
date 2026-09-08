using Backend.Api.Data;
using Backend.Api.Models.Dtos.Auth;
using Backend.Api.Models.Entities;
using Backend.Api.Services.Auth;
using Backend.Api.Services.Common;
using Backend.Api.Tests.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Moq;
using System.Security.Claims;

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
            var signInManager = TestHelpers.CreateMockSignInManager(userManager.Object);
            var jwtService = new Mock<IJwtTokenService>();
            var refreshTokenService = new Mock<IRefreshTokenService>();

            userManager
                .Setup(x => x.FindByEmailAsync(It.IsAny<string>()))
                .ReturnsAsync(new ApplicationUser
                {
                    Id = 1,
                    Email = "existing@example.com",
                    UserName = "existing@example.com"
                });

            var service = new AuthService(db, userManager.Object, signInManager.Object, jwtService.Object, refreshTokenService.Object);

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
        public async Task RegisterAsync_ShouldTrimEmailBeforeLookupAndCreation()
        {
            // Arrange
            using var db = TestHelpers.CreateInMemoryDbContext();
            var userManager = TestHelpers.CreateMockUserManager();
            var signInManager = TestHelpers.CreateMockSignInManager(userManager.Object);
            var jwtService = new Mock<IJwtTokenService>();
            var refreshTokenService = new Mock<IRefreshTokenService>();

            ApplicationUser? createdUser = null;

            userManager
                .Setup(x => x.FindByEmailAsync("new@example.com"))
                .ReturnsAsync((ApplicationUser?)null);

            userManager
                .Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>(), "Password123!"))
                .Callback<ApplicationUser, string>((user, _) => createdUser = user)
                .ReturnsAsync(IdentityResult.Success);

            var service = new AuthService(db, userManager.Object, signInManager.Object, jwtService.Object, refreshTokenService.Object);

            var dto = new RegisterRequestDto
            {
                Email = "  new@example.com  ",
                Password = "Password123!",
                DisplayName = "New User"
            };

            // Act
            var result = await service.RegisterAsync(dto);

            // Assert
            result.Succeeded.Should().BeTrue();

            userManager.Verify(x => x.FindByEmailAsync("new@example.com"), Times.Once);

            createdUser.Should().NotBeNull();
            createdUser!.Email.Should().Be("new@example.com");
            createdUser.UserName.Should().Be("new@example.com");
        }

        [Fact]
        public async Task RegisterAsync_ShouldTrimDisplayName()
        {
            // Arrange
            using var db = TestHelpers.CreateInMemoryDbContext();
            var userManager = TestHelpers.CreateMockUserManager();
            var signInManager = TestHelpers.CreateMockSignInManager(userManager.Object);
            var jwtService = new Mock<IJwtTokenService>();
            var refreshTokenService = new Mock<IRefreshTokenService>();

            userManager
                .Setup(x => x.FindByEmailAsync(It.IsAny<string>()))
                .ReturnsAsync((ApplicationUser?)null);

            userManager
                .Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
                .Callback<ApplicationUser, string>((user, _) => user.Id = 1)
                .ReturnsAsync(IdentityResult.Success);

            var service = new AuthService(db, userManager.Object, signInManager.Object, jwtService.Object, refreshTokenService.Object); 

            var dto = new RegisterRequestDto
            {
                Email = "display@example.com",
                Password = "Password123!",
                DisplayName = "  Display Name  "
            };

            // Act
            var result = await service.RegisterAsync(dto);

            // Assert
            result.Succeeded.Should().BeTrue();

            db.UserProfile.Should().ContainSingle();

            var profile = db.UserProfile.Single();

            profile.DisplayName.Should().Be("Display Name");
        }

        [Fact]
        public async Task RegisterAsync_ShouldStoreNullDisplayName_WhenDisplayNameIsBlank()
        {
            // Arrange
            using var db = TestHelpers.CreateInMemoryDbContext();
            var userManager = TestHelpers.CreateMockUserManager();
            var signInManager = TestHelpers.CreateMockSignInManager(userManager.Object);
            var jwtService = new Mock<IJwtTokenService>();
            var refreshTokenService = new Mock<IRefreshTokenService>();

            userManager
                .Setup(x => x.FindByEmailAsync(It.IsAny<string>()))
                .ReturnsAsync((ApplicationUser?)null);

            userManager
                .Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
                .Callback<ApplicationUser, string>((user, _) => user.Id = 1)
                .ReturnsAsync(IdentityResult.Success);

            var service = new AuthService(db, userManager.Object, signInManager.Object, jwtService.Object, refreshTokenService.Object);

            var dto = new RegisterRequestDto
            {
                Email = "blank@example.com",
                Password = "Password123!",
                DisplayName = "   "
            };

            // Act
            var result = await service.RegisterAsync(dto);

            // Assert
            result.Succeeded.Should().BeTrue();

            db.UserProfile.Should().ContainSingle();

            var profile = db.UserProfile.Single();

            profile.DisplayName.Should().BeNull();
        }

        [Fact]
        public async Task LoginAsync_ShouldFail_WhenPasswordIsInvalid()
        {
            // Arrange
            using var db = TestHelpers.CreateUnusedDbContext();
            var userManager = TestHelpers.CreateMockUserManager();
            var signInManager = TestHelpers.CreateMockSignInManager(userManager.Object);
            var jwtService = new Mock<IJwtTokenService>();
            var refreshTokenService = new Mock<IRefreshTokenService>();

            var user = new ApplicationUser
            {
                Id = 1,
                Email = "test@example.com",
                UserName = "test@example.com"
            };

            userManager
                .Setup(x => x.FindByEmailAsync(It.IsAny<string>()))
                .ReturnsAsync(user);

            signInManager
                .Setup(x => x.CheckPasswordSignInAsync(user, It.IsAny<string>(), true))
                .ReturnsAsync(SignInResult.Failed);

            var service = new AuthService(db, userManager.Object, signInManager.Object, jwtService.Object, refreshTokenService.Object);

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

            jwtService.Verify(x => x.CreateAccessToken(It.IsAny<ApplicationUser>()),Times.Never);
        }

        [Fact]
        public async Task LoginAsync_ShouldFail_WhenUserDoesNotExist()
        {
            // Arrange
            using var db = TestHelpers.CreateUnusedDbContext();
            var userManager = TestHelpers.CreateMockUserManager();
            var signInManager = TestHelpers.CreateMockSignInManager(userManager.Object);
            var jwtService = new Mock<IJwtTokenService>();
            var refreshTokenService = new Mock<IRefreshTokenService>();

            userManager
                .Setup(x => x.FindByEmailAsync(It.IsAny<string>()))
                .ReturnsAsync((ApplicationUser?)null);

            var service = new AuthService(db, userManager.Object, signInManager.Object, jwtService.Object, refreshTokenService.Object);

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
            var signInManager = TestHelpers.CreateMockSignInManager(userManager.Object);
            var jwtService = new Mock<IJwtTokenService>();
            var refreshTokenService = new Mock<IRefreshTokenService>();

            var user = new ApplicationUser
            {
                Id = 1,
                Email = "test@example.com",
                UserName = "test@example.com"
            };

            userManager
                .Setup(x => x.FindByEmailAsync(It.IsAny<string>()))
                .ReturnsAsync(user);

            signInManager
                .Setup(x => x.CheckPasswordSignInAsync(user, "Password123", true))
                .ReturnsAsync(SignInResult.Success);

            jwtService
                .Setup(x => x.CreateAccessToken(user))
                .Returns("fake-jwt-token");

            jwtService
                .SetupGet(x => x.AccessTokenLifetimeSeconds)
                .Returns(1800);

            var refreshTokenExpiresAt = DateTimeOffset.UtcNow.AddDays(14);
            var issuedRefreshToken = new IssuedRefreshToken("fake-refresh-token", refreshTokenExpiresAt);

            refreshTokenService
                .Setup(x => x.IssueAsync(user.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(ServiceResult<IssuedRefreshToken>.Ok(issuedRefreshToken));

            var service = new AuthService(db, userManager.Object, signInManager.Object, jwtService.Object, refreshTokenService.Object);

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
            result.Data.RefreshToken.Should().Be("fake-refresh-token");
            result.Data.RefreshTokenExpiresAtUtc.Should().Be(refreshTokenExpiresAt);

            jwtService.Verify(x => x.CreateAccessToken(user), Times.Once);
            refreshTokenService.Verify(x => x.IssueAsync(user.Id, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task LoginAsync_ShouldTrimEmailBeforeLookup()
        {
            // Arrange
            using var db = TestHelpers.CreateUnusedDbContext();
            var userManager = TestHelpers.CreateMockUserManager();
            var signInManager = TestHelpers.CreateMockSignInManager(userManager.Object);
            var jwtService = new Mock<IJwtTokenService>();
            var refreshTokenService = new Mock<IRefreshTokenService>();

            var user = new ApplicationUser
            {
                Id = 1,
                Email = "test@example.com",
                UserName = "test@example.com"
            };

            userManager
                .Setup(x => x.FindByEmailAsync("test@example.com"))
                .ReturnsAsync(user);

            signInManager
                .Setup(x => x.CheckPasswordSignInAsync(user, "Password123!", true))
                .ReturnsAsync(SignInResult.Success);

            jwtService
                .Setup(x => x.CreateAccessToken(user))
                .Returns("access-token");

            jwtService
                .SetupGet(x => x.AccessTokenLifetimeSeconds)
                .Returns(600);

            refreshTokenService
                .Setup(x => x.IssueAsync(user.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(ServiceResult<IssuedRefreshToken>.Ok(new IssuedRefreshToken("refresh-token", DateTimeOffset.UtcNow.AddDays(14))));

            var service = new AuthService(db, userManager.Object, signInManager.Object, jwtService.Object, refreshTokenService.Object);

            var dto = new LoginRequestDto
            {
                Email = "  test@example.com  ",
                Password = "Password123!"
            };

            // Act
            var result = await service.LoginAsync(dto);

            // Assert
            result.Succeeded.Should().BeTrue();

            userManager.Verify(x => x.FindByEmailAsync("test@example.com"), Times.Once);
        }

        [Fact]
        public async Task LoginAsync_ShouldFail_WhenRefreshTokenIssuingFails()
        {
            // Arrange
            using var db = TestHelpers.CreateUnusedDbContext();
            var userManager = TestHelpers.CreateMockUserManager();
            var signInManager = TestHelpers.CreateMockSignInManager(userManager.Object);
            var jwtService = new Mock<IJwtTokenService>();
            var refreshTokenService = new Mock<IRefreshTokenService>();

            var user = new ApplicationUser
            {
                Id = 1,
                Email = "test@example.com",
                UserName = "test@example.com"
            };

            userManager
                .Setup(x => x.FindByEmailAsync(It.IsAny<string>()))
                .ReturnsAsync(user);

            signInManager
                .Setup(x => x.CheckPasswordSignInAsync(user, "Password123", true))
                .ReturnsAsync(SignInResult.Success);

            refreshTokenService
                .Setup(x => x.IssueAsync(user.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(ServiceResult<IssuedRefreshToken>.Fail(new ServiceError("refresh_issue_failed", "Refresh token could not be issued.")));

            var service = new AuthService(db, userManager.Object, signInManager.Object, jwtService.Object, refreshTokenService.Object);

            var dto = new LoginRequestDto
            {
                Email = "test@example.com",
                Password = "Password123"
            };

            // Act
            var result = await service.LoginAsync(dto);

            // Assert
            result.Succeeded.Should().BeFalse();
            result.Errors.Should().ContainSingle(error => error.Code == "refresh_issue_failed");

            jwtService.Verify(x => x.CreateAccessToken(It.IsAny<ApplicationUser>()), Times.Never);
        }

        [Fact]
        public async Task RefreshAsync_ShouldReturnNewAuthResponse_WhenRotationSucceeds()
        {
            // Arrange
            using var db = TestHelpers.CreateUnusedDbContext();
            var userManager = TestHelpers.CreateMockUserManager();
            var signInManager = TestHelpers.CreateMockSignInManager(userManager.Object);
            var jwtService = new Mock<IJwtTokenService>();
            var refreshTokenService = new Mock<IRefreshTokenService>();

            var user = new ApplicationUser
            {
                Id = 1,
                Email = "test@example.com",
                UserName = "test@example.com"
            };

            var refreshTokenExpiresAt = DateTimeOffset.UtcNow.AddDays(14);

            refreshTokenService
                .Setup(x => x.RotateAsync("old-refresh-token", It.IsAny<CancellationToken>()))
                .ReturnsAsync(ServiceResult<RotatedRefreshToken>.Ok(new RotatedRefreshToken(user, "new-refresh-token", refreshTokenExpiresAt)));

            jwtService
                .Setup(x => x.CreateAccessToken(user))
                .Returns("new-access-token");

            jwtService
                .SetupGet(x => x.AccessTokenLifetimeSeconds)
                .Returns(600);

            var service = new AuthService(db, userManager.Object, signInManager.Object, jwtService.Object, refreshTokenService.Object);

            var dto = new RefreshTokenRequestDto
            {
                RefreshToken = "old-refresh-token"
            };

            // Act
            var result = await service.RefreshAsync(dto);

            // Assert
            result.Succeeded.Should().BeTrue();
            result.Data.Should().NotBeNull();

            result.Data!.AccessToken.Should().Be("new-access-token");
            result.Data.ExpiresInSeconds.Should().Be(600);
            result.Data.RefreshToken.Should().Be("new-refresh-token");
            result.Data.RefreshTokenExpiresAtUtc.Should().Be(refreshTokenExpiresAt);

            refreshTokenService.Verify(x => x.RotateAsync("old-refresh-token", It.IsAny<CancellationToken>()), Times.Once);

            jwtService.Verify(x => x.CreateAccessToken(user), Times.Once);
        }

        [Fact]
        public async Task RefreshAsync_ShouldFail_WhenRotationFails()
        {
            // Arrange
            using var db = TestHelpers.CreateUnusedDbContext();
            var userManager = TestHelpers.CreateMockUserManager();
            var signInManager = TestHelpers.CreateMockSignInManager(userManager.Object);
            var jwtService = new Mock<IJwtTokenService>();
            var refreshTokenService = new Mock<IRefreshTokenService>();

            refreshTokenService
                .Setup(x => x.RotateAsync("bad-refresh-token", It.IsAny<CancellationToken>()))
                .ReturnsAsync(ServiceResult<RotatedRefreshToken>.Fail(new ServiceError("invalid_refresh_token", "The refresh token is invalid or expired.")));

            var service = new AuthService(db, userManager.Object, signInManager.Object, jwtService.Object, refreshTokenService.Object);

            var dto = new RefreshTokenRequestDto
            {
                RefreshToken = "bad-refresh-token"
            };

            // Act
            var result = await service.RefreshAsync(dto);

            // Assert
            result.Succeeded.Should().BeFalse();
            result.Errors.Should().ContainSingle(error => error.Code == "invalid_refresh_token");

            jwtService.Verify(x => x.CreateAccessToken(It.IsAny<ApplicationUser>()), Times.Never);
        }

        [Fact]
        public async Task RevokeRefreshTokenAsync_ShouldFail_WhenUserClaimIsMissing()
        {
            // Arrange
            using var db = TestHelpers.CreateUnusedDbContext();
            var userManager = TestHelpers.CreateMockUserManager();
            var signInManager = TestHelpers.CreateMockSignInManager(userManager.Object);
            var jwtService = new Mock<IJwtTokenService>();
            var refreshTokenService = new Mock<IRefreshTokenService>();

            var service = new AuthService(db, userManager.Object, signInManager.Object, jwtService.Object, refreshTokenService.Object);

            var principal = new ClaimsPrincipal(new ClaimsIdentity());

            var dto = new RevokeRefreshTokenRequestDto
            {
                RefreshToken = "some-refresh-token-value-that-is-long-enough"
            };

            // Act
            var result = await service.RevokeRefreshTokenAsync(principal, dto);

            // Assert
            result.Succeeded.Should().BeFalse();
            result.Errors.Should().ContainSingle(error => error.Code == "unauthorized");

            refreshTokenService.Verify(x => x.RevokeFamilyAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RevokeRefreshTokenAsync_ShouldCallRefreshTokenService_WhenUserClaimIsValid()
        {
            // Arrange
            using var db = TestHelpers.CreateUnusedDbContext();
            var userManager = TestHelpers.CreateMockUserManager();
            var signInManager = TestHelpers.CreateMockSignInManager(userManager.Object);
            var jwtService = new Mock<IJwtTokenService>();
            var refreshTokenService = new Mock<IRefreshTokenService>();

            refreshTokenService
                .Setup(x => x.RevokeFamilyAsync(42, "some-refresh-token-value-that-is-long-enough", It.IsAny<CancellationToken>()))
                .ReturnsAsync(ServiceResult<bool>.Ok(true));

            var service = new AuthService(db, userManager.Object, signInManager.Object, jwtService.Object, refreshTokenService.Object);

            var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "42")
            ]));

            var dto = new RevokeRefreshTokenRequestDto
            {
                RefreshToken = "some-refresh-token-value-that-is-long-enough"
            };

            // Act
            var result = await service.RevokeRefreshTokenAsync(principal, dto);

            // Assert
            result.Succeeded.Should().BeTrue();

            refreshTokenService.Verify(x => x.RevokeFamilyAsync(42, "some-refresh-token-value-that-is-long-enough", It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetMeAsync_ShouldFail_WhenNameIdentifierClaimIsMissing()
        {
            // Arrange
            using var db = TestHelpers.CreateUnusedDbContext();
            var userManager = TestHelpers.CreateMockUserManager();
            var signInManager = TestHelpers.CreateMockSignInManager(userManager.Object);
            var jwtService = new Mock<IJwtTokenService>();
            var refreshTokenService = new Mock<IRefreshTokenService>();

            var service = new AuthService(db, userManager.Object, signInManager.Object, jwtService.Object, refreshTokenService.Object);

            var principal = new ClaimsPrincipal(new ClaimsIdentity());

            // Act
            var result = await service.GetMeAsync(principal);

            // Assert
            result.Succeeded.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.Code == "unauthorized");
        }

        [Fact]
        public async Task GetMeAsync_ShouldFail_WhenNameIdentifierClaimIsNotAnInteger()
        {
            // Arrange
            using var db = TestHelpers.CreateUnusedDbContext();
            var userManager = TestHelpers.CreateMockUserManager();
            var signInManager = TestHelpers.CreateMockSignInManager(userManager.Object);
            var jwtService = new Mock<IJwtTokenService>();
            var refreshTokenService = new Mock<IRefreshTokenService>();

            var service = new AuthService(db, userManager.Object, signInManager.Object, jwtService.Object, refreshTokenService.Object);

            var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, "not-an-integer")
            ]));

            // Act
            var result = await service.GetMeAsync(principal);

            // Assert
            result.Succeeded.Should().BeFalse();
            result.Errors.Should().ContainSingle(error => error.Code == "unauthorized");
        }
    }
}
