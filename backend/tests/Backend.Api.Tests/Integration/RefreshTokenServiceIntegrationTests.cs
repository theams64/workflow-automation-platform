using Backend.Api.Data;
using Backend.Api.Models.Entities;
using Backend.Api.Services.Auth;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace Backend.Api.Tests.Integration
{
    public sealed class RefreshTokenServiceIntegrationTests : IClassFixture<CustomWebApplicationFactory>
    {
        private readonly CustomWebApplicationFactory _factory;

        public RefreshTokenServiceIntegrationTests(CustomWebApplicationFactory factory)
        {
            _factory = factory;
        }

        [Fact]
        public async Task IssueAsync_ShouldStoreHash_NotRawToken()
        {
            await _factory.ResetDatabaseAsync();

            using var scope = _factory.Services.CreateScope();

            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var refreshTokens = scope.ServiceProvider.GetRequiredService<IRefreshTokenService>();

            var user = await CreateUserAsync(db);

            var result = await refreshTokens.IssueAsync(user.Id);

            result.Succeeded.Should().BeTrue();
            result.Data.Should().NotBeNull();
            result.Data!.RawToken.Should().NotBeNullOrWhiteSpace();

            var storedToken = db.RefreshToken.Single();

            storedToken.UserId.Should().Be(user.Id);
            storedToken.TokenHash.Should().NotBe(result.Data.RawToken);
            storedToken.TokenHash.Should().HaveLength(64);
            storedToken.RevokedAt.Should().BeNull();
            storedToken.ExpiresAt.Should().Be(result.Data.ExpiresAtUtc);

            var expectedHash = HashToken(result.Data.RawToken);

            storedToken.TokenHash.Should().Be(expectedHash);
        }

        [Fact]
        public async Task IssueAsync_ShouldDeleteOnlyCurrentUsersOldExpiredTokens()
        {
            await _factory.ResetDatabaseAsync();

            using var scope = _factory.Services.CreateScope();

            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var refreshTokens = scope.ServiceProvider.GetRequiredService<IRefreshTokenService>();

            var firstUser = await CreateUserAsync(db, "first-cleanup-user");
            var secondUser = await CreateUserAsync(db, "second-cleanup-user");

            var oldFirstUserToken = new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = firstUser.Id,
                TokenHash = HashToken("old-first-user-token"),
                FamilyId = Guid.NewGuid(),
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-60),
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(-45)
            };

            var oldSecondUserToken = new RefreshToken
            {
                Id = Guid.NewGuid(),
                UserId = secondUser.Id,
                TokenHash = HashToken("old-second-user-token"),
                FamilyId = Guid.NewGuid(),
                CreatedAt = DateTimeOffset.UtcNow.AddDays(-60),
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(-45)
            };

            db.RefreshToken.AddRange(oldFirstUserToken, oldSecondUserToken);

            await db.SaveChangesAsync();

            var issueResult = await refreshTokens.IssueAsync(firstUser.Id);

            issueResult.Succeeded.Should().BeTrue();

            var remainingTokens = db.RefreshToken.ToList();

            remainingTokens.Should().NotContain(token => token.Id == oldFirstUserToken.Id);
            remainingTokens.Should().Contain(token => token.Id == oldSecondUserToken.Id);

            remainingTokens.Should().Contain(token => token.UserId == firstUser.Id && token.RevokedAt == null && token.ExpiresAt > DateTimeOffset.UtcNow);
        }

        [Fact]
        public async Task RotateAsync_ShouldRevokeOldToken_AndCreateReplacement()
        {
            await _factory.ResetDatabaseAsync();

            using var scope = _factory.Services.CreateScope();

            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var refreshTokens = scope.ServiceProvider.GetRequiredService<IRefreshTokenService>();

            var user = await CreateUserAsync(db);

            var issueResult = await refreshTokens.IssueAsync(user.Id);
            issueResult.Succeeded.Should().BeTrue();

            var originalRawToken = issueResult.Data!.RawToken;

            var rotateResult = await refreshTokens.RotateAsync(originalRawToken);

            rotateResult.Succeeded.Should().BeTrue();
            rotateResult.Data.Should().NotBeNull();
            rotateResult.Data!.User.Id.Should().Be(user.Id);
            rotateResult.Data.RawToken.Should().NotBeNullOrWhiteSpace();
            rotateResult.Data.RawToken.Should().NotBe(originalRawToken);

            var tokens = db.RefreshToken.AsNoTracking().OrderBy(token => token.CreatedAt).ToList();

            tokens.Should().HaveCount(2);

            var originalStoredToken = tokens[0];
            var replacementStoredToken = tokens[1];

            originalStoredToken.RevokedAt.Should().NotBeNull();
            originalStoredToken.ReplacedByTokenId.Should().Be(replacementStoredToken.Id);

            replacementStoredToken.RevokedAt.Should().BeNull();
            replacementStoredToken.FamilyId.Should().Be(originalStoredToken.FamilyId);
            replacementStoredToken.TokenHash.Should().Be(HashToken(rotateResult.Data.RawToken));
        }

        [Fact]
        public async Task RotateAsync_ShouldFail_WhenRefreshTokenIsExpired()
        {
            await _factory.ResetDatabaseAsync();

            using var scope = _factory.Services.CreateScope();

            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var refreshTokens = scope.ServiceProvider.GetRequiredService<IRefreshTokenService>();

            var user = await CreateUserAsync(db);

            var issueResult = await refreshTokens.IssueAsync(user.Id);
            issueResult.Succeeded.Should().BeTrue();

            var storedToken = db.RefreshToken.Single();
            storedToken.ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(-1);

            await db.SaveChangesAsync();

            var rotateResult = await refreshTokens.RotateAsync(issueResult.Data!.RawToken);

            rotateResult.Succeeded.Should().BeFalse();
            rotateResult.Errors.Should().Contain(error => error.Code == "invalid_refresh_token");

            db.RefreshToken.AsNoTracking().Single().RevokedAt.Should().NotBeNull();
        }

        [Fact]
        public async Task RotateAsync_ShouldRevokeFamily_WhenOldTokenIsReused()
        {
            await _factory.ResetDatabaseAsync();

            using var scope = _factory.Services.CreateScope();

            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var refreshTokens = scope.ServiceProvider.GetRequiredService<IRefreshTokenService>();

            var user = await CreateUserAsync(db);

            var issueResult = await refreshTokens.IssueAsync(user.Id);
            issueResult.Succeeded.Should().BeTrue();

            var originalRawToken = issueResult.Data!.RawToken;

            var firstRotation = await refreshTokens.RotateAsync(originalRawToken);

            firstRotation.Succeeded.Should().BeTrue();
            firstRotation.Data.Should().NotBeNull();

            var reuseResult = await refreshTokens.RotateAsync(originalRawToken);

            reuseResult.Succeeded.Should().BeFalse();
            reuseResult.Errors.Should().Contain(error => error.Code == "refresh_token_reused");

            var useCurrentFamilyTokenAfterReuse = await refreshTokens.RotateAsync(firstRotation.Data!.RawToken);

            useCurrentFamilyTokenAfterReuse.Succeeded.Should().BeFalse();
            useCurrentFamilyTokenAfterReuse.Errors.Should().Contain(error => error.Code == "refresh_token_reused" || error.Code == "invalid_refresh_token");

            var tokens = db.RefreshToken.AsNoTracking().ToList();

            tokens.Should().HaveCount(2);
            tokens.Should().OnlyContain(token => token.RevokedAt != null);
        }

        [Fact]
        public async Task RotateAsync_ShouldAllowOnlyOneSuccessfulConcurrentRotation()
        {
            await _factory.ResetDatabaseAsync();

            string rawToken;

            using (var setupScope = _factory.Services.CreateScope())
            {
                var db = setupScope.ServiceProvider.GetRequiredService<AppDbContext>();
                var refreshTokens = setupScope.ServiceProvider.GetRequiredService<IRefreshTokenService>();

                var user = await CreateUserAsync(db);

                var issueResult = await refreshTokens.IssueAsync(user.Id);

                issueResult.Succeeded.Should().BeTrue();

                rawToken = issueResult.Data!.RawToken;
            }

            var firstTask = Task.Run(async () =>
            {
                using var scope = _factory.Services.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IRefreshTokenService>();

                return await service.RotateAsync(rawToken);
            });

            var secondTask = Task.Run(async () =>
            {
                using var scope = _factory.Services.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IRefreshTokenService>();

                return await service.RotateAsync(rawToken);
            });

            var results = await Task.WhenAll(firstTask, secondTask);

            results.Should().ContainSingle(result => result.Succeeded);
            results.Should().ContainSingle(result => !result.Succeeded);

            using var verificationScope = _factory.Services.CreateScope();
            var verificationDb = verificationScope.ServiceProvider.GetRequiredService<AppDbContext>();

            var tokens = verificationDb.RefreshToken.ToList();

            tokens.Should().HaveCount(2);

            var activeTokens = tokens
                .Where(token => token.RevokedAt is null && token.ExpiresAt > DateTimeOffset.UtcNow)
                .ToList();

            activeTokens.Should().HaveCountLessThanOrEqualTo(1);
        }

        [Fact]
        public async Task RevokeFamilyAsync_ShouldRevokeOnlyMatchingUsersTokenFamily()
        {
            await _factory.ResetDatabaseAsync();

            using var scope = _factory.Services.CreateScope();

            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var refreshTokens = scope.ServiceProvider.GetRequiredService<IRefreshTokenService>();

            var firstUser = await CreateUserAsync(db, "first");
            var secondUser = await CreateUserAsync(db, "second");

            var firstToken = await refreshTokens.IssueAsync(firstUser.Id);
            var secondToken = await refreshTokens.IssueAsync(secondUser.Id);

            firstToken.Succeeded.Should().BeTrue();
            secondToken.Succeeded.Should().BeTrue();

            var wrongUserResult = await refreshTokens.RevokeFamilyAsync(secondUser.Id, firstToken.Data!.RawToken);

            wrongUserResult.Succeeded.Should().BeTrue();

            var firstUsersStoredToken = db.RefreshToken.AsNoTracking().Single(token => token.UserId == firstUser.Id);

            firstUsersStoredToken.RevokedAt.Should().BeNull();

            var correctUserResult = await refreshTokens.RevokeFamilyAsync(firstUser.Id, firstToken.Data.RawToken);

            correctUserResult.Succeeded.Should().BeTrue();

            firstUsersStoredToken = db.RefreshToken.AsNoTracking().Single(token => token.UserId == firstUser.Id);

            firstUsersStoredToken.RevokedAt.Should().NotBeNull();

            var secondUsersStoredToken = db.RefreshToken.AsNoTracking().Single(token => token.UserId == secondUser.Id);

            secondUsersStoredToken.RevokedAt.Should().BeNull();
        }

        [Fact]
        public async Task IssueAsync_ShouldEnforceMaximumActiveSessionsPerUser()
        {
            await _factory.ResetDatabaseAsync();

            using var scope = _factory.Services.CreateScope();

            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var refreshTokens = scope.ServiceProvider.GetRequiredService<IRefreshTokenService>();

            var user = await CreateUserAsync(db);

            for (var i = 0; i < 6; i++)
            {
                var issueResult = await refreshTokens.IssueAsync(user.Id);

                issueResult.Succeeded.Should().BeTrue();
            }

            var activeTokens = db.RefreshToken
                .Where(token => token.UserId == user.Id && token.RevokedAt == null && token.ExpiresAt > DateTimeOffset.UtcNow)
                .ToList();

            activeTokens.Should().HaveCount(5);

            var revokedTokens = db.RefreshToken
                .Where(token => token.UserId == user.Id && token.RevokedAt != null)
                .ToList();

            revokedTokens.Should().HaveCount(1);
        }

        private static async Task<ApplicationUser> CreateUserAsync(AppDbContext db, string prefix = "user")
        {
            var unique = Guid.NewGuid().ToString("N");

            var user = new ApplicationUser
            {
                Email = $"{prefix}-{unique}@example.com",
                UserName = $"{prefix}-{unique}@example.com",
                NormalizedEmail = $"{prefix}-{unique}@example.com".ToUpperInvariant(),
                NormalizedUserName = $"{prefix}-{unique}@example.com".ToUpperInvariant()
            };

            db.Users.Add(user);

            await db.SaveChangesAsync();

            return user;
        }

        private static string HashToken(string rawToken)
        {
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
        }
    }
}