using Backend.Api.Configuration;
using Backend.Api.Models.Entities;
using Backend.Api.Services.WorkflowExecution;
using Backend.Api.Tests.Common;
using Backend.Api.WorkflowEngine.Composition;
using Backend.Api.WorkflowEngine.Connections;
using Backend.Api.WorkflowEngine.Execution;
using Backend.Api.WorkflowEngine.Expressions;
using Backend.Api.WorkflowEngine.Http;
using Backend.Api.WorkflowEngine.Http.Level1;
using Backend.Api.WorkflowEngine.References;
using Backend.Api.WorkflowEngine.Registry;
using Backend.Api.WorkflowEngine.Slack;
using Backend.Api.WorkflowEngine.Time;
using Backend.Api.WorkflowEngine.Transform;
using Backend.Api.WorkflowEngine.Validation;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using System.Text.Json;

namespace Backend.Api.Tests.WorkflowEngine
{
    public sealed class Checkpoint3WorkflowExecutionTests
    {
        private const string FakeWebhook = "https://hooks.slack.com/services/T000/B000/FAKE_WEBHOOK_SECRET";

        [Fact]
        public async Task ExecuteAsync_ShouldRunLevel1TransformComposeAndSlackThroughGenericRunner()
        {
            await using var db = WorkflowTestHelpers.CreateInMemoryDbContext();

            var connection = new ManagedConnection
            {
                ID = Guid.NewGuid(),
                UserID = 7,
                Name = "Slack",
                ConnectionType = ManagedConnectionTypes.SlackWebhook,
                CanonicalOrigin = "https://hooks.slack.com",
                CredentialType = ManagedCredentialTypes.SlackWebhookUrl,
                SecretReference = "slack-test",
                CredentialPlacement = ManagedCredentialPlacements.Uri,
                IsEnabled = true
            };

            var workflow = new Workflow
            {
                ID = 1,
                UserID = 7,
                Name = "Events digest",
                IsEnabled = true,
                Timezone = "America/Chicago"
            };

            var steps = new[]
            {
                new WorkflowStep
                {
                    ID = 1,
                    WorkflowID = 1,
                    StepKey = "fetch",
                    StepType = Level1HttpStepExecutor.StepTypeName,
                    StepOrder = 1,
                    ConfigJson =
                        """
                        {
                          "originId":"test-origin",
                          "path":"/events"
                        }
                        """
                },
                new WorkflowStep
                {
                    ID = 2,
                    WorkflowID = 1,
                    StepKey = "transform",
                    StepType = TransformStepExecutor.StepTypeName,
                    StepOrder = 2,
                    ConfigJson =
                        """
                        {
                          "output": {
                            "location":"{{ workflow.inputs.location }}",
                            "eventCount":"{{ count(steps.fetch.output.body.events) }}",
                            "events": {
                              "$map": {
                                "source":"{{ steps.fetch.output.body.events }}",
                                "maximumItems":10,
                                "item": {
                                  "title":"{{ item.name }}",
                                  "venue":"{{ default(item.venue.name, \"Venue unavailable\") }}",
                                  "time":"{{ formatDateTime(item.start, \"h:mm tt\", execution.timezone) }}",
                                  "priceText":"{{ priceText(item.price.minimum, item.price.currency) }}"
                                }
                              }
                            }
                          }
                        }
                        """
                },
                new WorkflowStep
                {
                    ID = 3,
                    WorkflowID = 1,
                    StepKey = "compose",
                    StepType = MessageCompositionStepExecutor.StepTypeName,
                    StepOrder = 3,
                    ConfigJson =
                        """
                        {
                          "titleTemplate":"Events in {{ steps.transform.output.location }}",
                          "emptyMessageTemplate":"No matching events were found.",
                          "collectionReference":"{{ steps.transform.output.events }}",
                          "itemTemplate":"- {{ item.title }} — {{ item.time }} at {{ item.venue }}. {{ item.priceText }}",
                          "maximumItems":10,
                          "separator":"\n"
                        }
                        """
                },
                new WorkflowStep
                {
                    ID = 4,
                    WorkflowID = 1,
                    StepKey = "notify",
                    StepType = SlackNotificationStepExecutor.StepTypeName,
                    StepOrder = 4,
                    ConfigJson = JsonSerializer.Serialize(new
                        {
                            connectionId = connection.ID,
                            message = "{{steps.compose.output.text}}"
                        })
                }
            };

            db.ManagedConnection.Add(connection);
            db.Workflow.Add(workflow);
            db.WorkflowStep.AddRange(steps);
            await db.SaveChangesAsync();

            var safeHttp = new Mock<ISafeOutboundHttpClient>();
            safeHttp
                .Setup(client => client.SendAsync(It.IsAny<SafeHttpRequest>(), It.IsAny<OutboundRequestPolicy>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(
                    new SafeHttpResponse(
                        200,
                        "application/json",
                        JsonSerializer.SerializeToElement(new
                        {
                            events = new[]
                            {
                                new
                                {
                                    name = "Example Concert",
                                    start = "2026-01-01T19:30:00-05:00",
                                    venue = new { name = "Example Arena" },
                                    price = new
                                    {
                                        minimum = 42.5m,
                                        currency = "USD"
                                    }
                                }
                            }
                        }),
                        new Dictionary<string, string>(),
                        128));

            var origins = new ApprovedHttpOriginsOptions();
            origins.Origins["test-origin"] = new ApprovedHttpOriginOptions
            {
                BaseUri = "https://api.example.com",
                AllowedMethods = ["GET"],
                AllowedPathPrefixes = ["/events"]
            };

            var safeOptions = Options.Create(new SafeHttpOptions());
            var parser = new WorkflowReferenceParser();
            var resolver = new WorkflowReferenceResolver();
            var level1 = new Level1HttpStepExecutor(new ApprovedHttpOriginCatalog(Options.Create(origins), safeOptions), new Level1HttpRequestMaterializer(parser, resolver, safeOptions), safeHttp.Object);

            var expressionOptions = Options.Create(new WorkflowExpressionOptions());
            var expressions = new WorkflowExpressionEngine(parser, resolver, new TimezoneValidator(), expressionOptions);
            var transformProcessor = new TransformTemplateProcessor(expressions, expressionOptions, Options.Create(new TransformOptions()));
            var transform = new TransformStepExecutor(transformProcessor);
            var compose = new MessageCompositionStepExecutor(expressions, expressionOptions, Options.Create(new MessageCompositionOptions()));

            var slackTransport = new RecordingSlackWebhookClient();
            var slack = new SlackNotificationStepExecutor(
                new ManagedConnectionRuntimeResolver(db),
                new DictionaryConnectionSecretProvider(
                    new Dictionary<string, string>
                    {
                        ["slack-test"] = FakeWebhook
                    }),
                expressions,
                slackTransport,
                Options.Create(new SlackDeliveryOptions()));

            var registry = new StepExecutorRegistry([level1, transform, compose, slack]);
            var validation = new WorkflowValidationService(registry, parser);
            var clock = new FakeClock(DateTimeOffset.Parse("2026-01-01T12:00:00Z"));
            var runner = new WorkflowRunner(db, registry, clock, Options.Create(new WorkflowExecutionOptions { StepTimeoutSeconds = 5, WorkflowTimeoutSeconds = 30 }));
            var service = new WorkflowExecutionService(db, WorkflowTestHelpers.CreateCurrentUserServiceMock(7).Object, validation, runner, new ExecutionDateResolver(clock, new TimezoneValidator()), clock);

            using var inputs = JsonDocument.Parse("""{"location":"Chicago"}""");

            var result = await service.ExecuteAsync(workflow.ID, WorkflowTriggerTypes.Manual, inputs.RootElement, cancellationToken: CancellationToken.None);

            result.Succeeded.Should().BeTrue();
            result.Data!.Status.Should().Be(ExecutionStatuses.Succeeded);

            var records = db.StepExecution
                .OrderBy(record => record.StepOrder)
                .ToList();

            records.Should().HaveCount(4);
            records.Should().OnlyContain(record => record.Status == ExecutionStatuses.Succeeded);

            using var transformOutput = JsonDocument.Parse(records[1].OutputJson!);
            transformOutput.RootElement.GetProperty("eventCount").GetInt32().Should().Be(1);
            transformOutput.RootElement.GetProperty("events")[0].GetProperty("title").GetString().Should().Be("Example Concert");

            using var composeOutput = JsonDocument.Parse(records[2].OutputJson!);
            var composedText = composeOutput.RootElement.GetProperty("text") .GetString();
            composedText.Should().Contain("Events in Chicago");
            composedText.Should().Contain("Example Concert");

            using var slackOutput = JsonDocument.Parse(records[3].OutputJson!);
            slackOutput.RootElement.GetProperty("delivered").GetBoolean().Should().BeTrue();

            slackTransport.Requests.Should().ContainSingle();
            slackTransport.Requests[0].Text.Should().Be(composedText);

            var persistedText = string.Join("\n", db.WorkflowExecution.Select(x => $"{x.InputJson} {x.ErrorMessage}").Concat(db.StepExecution.Select(x => $"{x.OutputJson} {x.ErrorMessage}")));

            persistedText.Should().NotContain("FAKE_WEBHOOK_SECRET");
            persistedText.Should().NotContain(FakeWebhook);
        }

        [Fact]
        public async Task GenericRunner_ShouldStopBeforeSlack_WhenCompositionFails()
        {
            await using var db = WorkflowTestHelpers.CreateInMemoryDbContext();
            var laterExecuted = false;

            var source = new FakeStepExecutor("source", execute: (_, _) => Task.FromResult(StepExecutionResult.Success(NormalizedStepOutput.FromValue(new { value = 1 }))));
            var failingComposition = new FakeStepExecutor(MessageCompositionStepExecutor.StepTypeName, execute: (_, _) => Task.FromResult(StepExecutionResult.Failure(ExecutionErrorCodes.MessageSizeExceeded, "The composed message exceeded the allowed size.")));
            var slack = new FakeStepExecutor(
                SlackNotificationStepExecutor.StepTypeName,
                execute: (_, _) =>
                {
                    laterExecuted = true;
                    return Task.FromResult(StepExecutionResult.Success());
                });

            var registry = new StepExecutorRegistry([source, failingComposition, slack]);
            var clock = new FakeClock(DateTimeOffset.Parse("2026-01-01T12:00:00Z"));

            var workflow = new Workflow
            {
                ID = 1,
                UserID = 7,
                Name = "Failure",
                IsEnabled = true,
                Timezone = "UTC"
            };
            var steps = new[]
            {
                new WorkflowStep
                {
                    ID = 1,
                    WorkflowID = 1,
                    StepKey = "source",
                    StepType = "source",
                    StepOrder = 1,
                    ConfigJson = "{}"
                },
                new WorkflowStep
                {
                    ID = 2,
                    WorkflowID = 1,
                    StepKey = "compose",
                    StepType = MessageCompositionStepExecutor.StepTypeName,
                    StepOrder = 2,
                    ConfigJson = "{}"
                },
                new WorkflowStep
                {
                    ID = 3,
                    WorkflowID = 1,
                    StepKey = "notify",
                    StepType = SlackNotificationStepExecutor.StepTypeName,
                    StepOrder = 3,
                    ConfigJson = "{}"
                }
            };
            var execution = new WorkflowExecution
            {
                ID = Guid.NewGuid(),
                WorkflowID = 1,
                InitiatingUserID = 7,
                TriggerType = WorkflowTriggerTypes.Manual,
                Status = ExecutionStatuses.Pending,
                CreatedAt = clock.UtcNow,
                EffectiveDate = new DateOnly(2026, 1, 1),
                Timezone = "UTC",
                InputJson = "{}"
            };

            db.Workflow.Add(workflow);
            db.WorkflowStep.AddRange(steps);
            db.WorkflowExecution.Add(execution);
            await db.SaveChangesAsync();

            var runner = new WorkflowRunner(db, registry, clock, Options.Create(new WorkflowExecutionOptions()));
            using var inputs = JsonDocument.Parse("{}");

            var result = await runner.RunAsync(new WorkflowRunRequest(workflow, steps, execution, inputs.RootElement), CancellationToken.None);

            result.Status.Should().Be(ExecutionStatuses.Failed);
            laterExecuted.Should().BeFalse();
            db.StepExecution.Should().HaveCount(2);
        }
    }
}
