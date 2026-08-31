using Backend.Api.Models.Dtos.ManagedConnection;
using Backend.Api.Models.Entities;
using Backend.Api.Services.ManagedConnection;
using Backend.Api.Services.ManagedConnections;
using Backend.Api.Tests.Common;
using Backend.Api.WorkflowEngine.Connections;
using FluentAssertions;
using System.Text.Json;

namespace Backend.Api.Tests.Services
{
    public sealed class ManagedConnectionServiceTests
    {
        private const string FakeWebhook = "https://hooks.slack.com/services/T000/B000/FAKE_WEBHOOK_SECRET";

        [Fact]
        public async Task CreateAsync_ShouldPersistOnlySecretReferenceAndReturnSanitizedDto()
        {
            await using var db = WorkflowTestHelpers.CreateInMemoryDbContext();
            var secretProvider = new DictionaryConnectionSecretProvider(
                new Dictionary<string, string>
                {
                    ["slack-test"] = FakeWebhook
                });
            var clock = new FakeClock(DateTimeOffset.Parse("2026-01-01T12:00:00Z"));

            var sut = new ManagedConnectionService(db, WorkflowTestHelpers.CreateCurrentUserServiceMock(7).Object, secretProvider, clock);

            var result = await sut.CreateAsync(
                new CreateManagedConnectionRequestDto
                {
                    Name = "Slack",
                    ConnectionType = ManagedConnectionTypes.SlackWebhook,
                    SecretReference = "slack-test"
                });

            result.Succeeded.Should().BeTrue();
            result.Data.Should().NotBeNull();

            var persisted = db.ManagedConnection.Single();
            persisted.SecretReference.Should().Be("slack-test");
            JsonSerializer.Serialize(persisted).Should().NotContain("FAKE_WEBHOOK_SECRET");

            var responseJson = JsonSerializer.Serialize(result.Data);
            responseJson.Should().NotContain("slack-test");
            responseJson.Should().NotContain("FAKE_WEBHOOK_SECRET");
        }

        [Fact]
        public async Task GetByIdAsync_ShouldReturnNotFound_ForOtherUsersConnection()
        {
            await using var db = WorkflowTestHelpers.CreateInMemoryDbContext();

            var connection = Connection(userId: 99);
            db.ManagedConnection.Add(connection);
            await db.SaveChangesAsync();

            var sut = new ManagedConnectionService(db, WorkflowTestHelpers.CreateCurrentUserServiceMock(7).Object, new DictionaryConnectionSecretProvider(new Dictionary<string, string>()), new FakeClock(DateTimeOffset.Parse("2026-01-01T12:00:00Z")));

            var result = await sut.GetByIdAsync(connection.ID);

            result.Succeeded.Should().BeFalse();
            result.Errors.Should().ContainSingle(error => error.Code == "connection.not_found");
        }

        [Fact]
        public async Task RevokeAsync_ShouldDisableConnectionAndSetRevokedAt()
        {
            await using var db = WorkflowTestHelpers.CreateInMemoryDbContext();

            var connection = Connection(userId: 7);
            db.ManagedConnection.Add(connection);
            await db.SaveChangesAsync();

            var now = DateTimeOffset.Parse("2026-01-01T12:00:00Z");
            var sut = new ManagedConnectionService(db, WorkflowTestHelpers.CreateCurrentUserServiceMock(7).Object, new DictionaryConnectionSecretProvider(new Dictionary<string, string>()), new FakeClock(now));

            var result = await sut.RevokeAsync(connection.ID);

            result.Succeeded.Should().BeTrue();

            var persisted = db.ManagedConnection.Single();
            persisted.IsEnabled.Should().BeFalse();
            persisted.RevokedAt.Should().Be(now);
        }

        private static ManagedConnection Connection(int userId) => new()
        {
            ID = Guid.NewGuid(),
            UserID = userId,
            Name = "Slack",
            ConnectionType = ManagedConnectionTypes.SlackWebhook,
            CanonicalOrigin = "https://hooks.slack.com",
            CredentialType = ManagedCredentialTypes.SlackWebhookUrl,
            SecretReference = "slack-test",
            CredentialPlacement = ManagedCredentialPlacements.Uri,
            IsEnabled = true
        };
    }
}
