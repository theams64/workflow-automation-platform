using Backend.Api.Configuration;
using Backend.Api.Models.Entities;
using Backend.Api.Tests.Common;
using Backend.Api.WorkflowEngine.Connections;
using Backend.Api.WorkflowEngine.Execution;
using Backend.Api.WorkflowEngine.Expressions;
using Backend.Api.WorkflowEngine.References;
using Backend.Api.WorkflowEngine.Slack;
using Backend.Api.WorkflowEngine.Time;
using Backend.Api.WorkflowEngine.Validation;
using FluentAssertions;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Backend.Api.Tests.WorkflowEngine
{
    public sealed class SlackNotificationStepExecutorTests
    {
        private const string FakeWebhook = "https://hooks.slack.com/services/T000/B000/FAKE_WEBHOOK_SECRET";

        [Fact]
        public async Task ExecuteAsync_ShouldDeliverLiteralTextWithoutPriorStep()
        {
            await using var db = WorkflowTestHelpers.CreateInMemoryDbContext();
            var connection = Connection(userId: 7);
            db.ManagedConnection.Add(connection);
            await db.SaveChangesAsync();

            var slack = new RecordingSlackWebhookClient();
            var sut = CreateExecutor(
                db,
                new DictionaryConnectionSecretProvider(
                    new Dictionary<string, string>
                    {
                        ["slack-test"] = FakeWebhook
                    }),
                slack);

            var context = CreateContext(connection.ID, userId: 7, message: "Hello from the workflow");

            var result = await sut.ExecuteAsync(context, CancellationToken.None);

            result.Succeeded.Should().BeTrue();
            result.Output!.Value.GetProperty("delivered").GetBoolean().Should().BeTrue();
            result.Output.Json.Should().NotContain("FAKE_WEBHOOK_SECRET");
            slack.Requests.Should().ContainSingle();
            slack.Requests[0].Text.Should().Be("Hello from the workflow");
        }

        [Fact]
        public async Task ExecuteAsync_ShouldRenderWorkflowInputTemplate()
        {
            await using var db = WorkflowTestHelpers.CreateInMemoryDbContext();
            var connection = Connection(userId: 7);
            db.ManagedConnection.Add(connection);
            await db.SaveChangesAsync();

            var slack = new RecordingSlackWebhookClient();
            var sut = CreateExecutor(
                db,
                new DictionaryConnectionSecretProvider(
                    new Dictionary<string, string>
                    {
                        ["slack-test"] = FakeWebhook
                    }),
                slack);

            var context = CreateContext(connection.ID, userId: 7, message: "Hello {{ workflow.inputs.name }}", workflowInputs: """{"name":"Ada"}""");

            var result = await sut.ExecuteAsync(context, CancellationToken.None);

            result.Succeeded.Should().BeTrue();
            slack.Requests.Should().ContainSingle();
            slack.Requests[0].Text.Should().Be("Hello Ada");
        }

        [Fact]
        public async Task ExecuteAsync_ShouldAcceptCompatiblePriorStepText()
        {
            await using var db = WorkflowTestHelpers.CreateInMemoryDbContext();
            var connection = Connection(userId: 7);
            db.ManagedConnection.Add(connection);
            await db.SaveChangesAsync();

            var slack = new RecordingSlackWebhookClient();
            var sut = CreateExecutor(
                db,
                new DictionaryConnectionSecretProvider(
                    new Dictionary<string, string>
                    {
                        ["slack-test"] = FakeWebhook
                    }),
                slack);

            var context = CreateContext(connection.ID, userId: 7, message: "{{ steps.producer.output.summary }}");

            context.WorkflowContext.AddStepOutput(
                "producer",
                NormalizedStepOutput.FromValue(new
                {
                    summary = "Produced elsewhere"
                }));

            var result = await sut.ExecuteAsync(context, CancellationToken.None);

            result.Succeeded.Should().BeTrue();
            slack.Requests[0].Text.Should().Be("Produced elsewhere");
        }

        [Fact]
        public async Task ExecuteAsync_ShouldRejectCrossUserConnectionWithoutMaterializingSecret()
        {
            await using var db = WorkflowTestHelpers.CreateInMemoryDbContext();
            var connection = Connection(userId: 99);
            db.ManagedConnection.Add(connection);
            await db.SaveChangesAsync();

            var slack = new RecordingSlackWebhookClient();
            var sut = CreateExecutor(db, new DictionaryConnectionSecretProvider(new Dictionary<string, string>()), slack);
            var context = CreateContext(connection.ID, userId: 7, message: "Hello");

            var result = await sut.ExecuteAsync(context, CancellationToken.None);

            result.Succeeded.Should().BeFalse();
            result.ErrorCode.Should().Be(ExecutionErrorCodes.InvalidConnection);
            slack.Requests.Should().BeEmpty();
        }

        [Fact]
        public async Task ExecuteAsync_ShouldRejectRevokedConnection()
        {
            await using var db = WorkflowTestHelpers.CreateInMemoryDbContext();
            var connection = Connection(userId: 7);
            connection.RevokedAt = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
            connection.IsEnabled = false;
            db.ManagedConnection.Add(connection);
            await db.SaveChangesAsync();

            var sut = CreateExecutor(db, new DictionaryConnectionSecretProvider(new Dictionary<string, string> { ["slack-test"] = FakeWebhook }), new RecordingSlackWebhookClient());
            var context = CreateContext(connection.ID, userId: 7, message: "Hello");

            var result = await sut.ExecuteAsync(context, CancellationToken.None);

            result.Succeeded.Should().BeFalse();
            result.ErrorCode.Should().Be(ExecutionErrorCodes.ConnectionRevoked);
        }

        [Fact]
        public void ValidateConfiguration_ShouldRejectItemReferenceOutsideControlledIteration()
        {
            var expressions = CreateExpressions();
            var sut = new SlackNotificationStepExecutor(null!, null!, expressions, null!, Options.Create(new SlackDeliveryOptions()));

            var result = sut.ValidateConfiguration(
                """
                {
                  "connectionId":"11111111-2222-3333-4444-555555555555",
                  "message":"{{ item.name }}"
                }
                """,
                new WorkflowValidationContext(1, new Dictionary<string, StepOutputSchema>()));

            result.Should().Contain(error => error.Code == "workflow_step.slack_message_invalid");
        }

        [Fact]
        public void ValidateConfiguration_ShouldRejectForwardReference()
        {
            var expressions = CreateExpressions();
            var sut = new SlackNotificationStepExecutor(null!, null!, expressions, null!, Options.Create(new SlackDeliveryOptions()));

            var result = sut.ValidateConfiguration(
                """
                {
                  "connectionId":"11111111-2222-3333-4444-555555555555",
                  "message":"{{ steps.later.output.text }}"
                }
                """,
                new WorkflowValidationContext(1, new Dictionary<string, StepOutputSchema>()));

            result.Should().Contain(error => error.Code == "workflow_step.reference_invalid");
            result.Should().Contain(error => error.Message.Contains("a reference targets an unknown or later step"));
        }

        private static SlackNotificationStepExecutor CreateExecutor(Backend.Api.Data.AppDbContext db, IConnectionSecretProvider secretProvider, ISlackWebhookClient slackClient) =>
            new(
                new ManagedConnectionRuntimeResolver(db),
                secretProvider,
                CreateExpressions(),
                slackClient,
                Options.Create(new SlackDeliveryOptions())
            );

        private static WorkflowExpressionEngine CreateExpressions() =>
            new(
                new WorkflowReferenceParser(),
                new WorkflowReferenceResolver(),
                new TimezoneValidator(),
                Options.Create(new WorkflowExpressionOptions())
            );

        private static StepExecutionContext CreateContext(Guid connectionId, int userId, string message, string workflowInputs = "{}")
        {
            using var inputs = JsonDocument.Parse(workflowInputs);

            return new StepExecutionContext(
                new Workflow
                {
                    ID = 1,
                    UserID = userId,
                    Name = "Slack",
                    IsEnabled = true,
                    Timezone = "UTC"
                },
                new WorkflowStep
                {
                    ID = 4,
                    WorkflowID = 1,
                    StepKey = "notify",
                    StepType = SlackNotificationStepExecutor.StepTypeName,
                    StepOrder = 1,
                    ConfigJson = JsonSerializer.Serialize(new
                    {
                        connectionId,
                        message
                    })
                },
                new WorkflowExecutionContext(
                    new WorkflowExecutionMetadata(
                        Guid.NewGuid(),
                        WorkflowTriggerTypes.Manual,
                        DateTimeOffset.Parse("2026-01-01T12:00:00Z"),
                        null,
                        new DateOnly(2026, 1, 1),
                        "UTC"),
                    inputs.RootElement));
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
