using Backend.Api.Configuration;
using Backend.Api.Models.Entities;
using Backend.Api.Services.WorkflowExecution;
using Backend.Api.Tests.Common;
using Backend.Api.WorkflowEngine.Execution;
using Backend.Api.WorkflowEngine.Http;
using Backend.Api.WorkflowEngine.Http.Level1;
using Backend.Api.WorkflowEngine.References;
using Backend.Api.WorkflowEngine.Registry;
using Backend.Api.WorkflowEngine.Time;
using Backend.Api.WorkflowEngine.Validation;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using System.Text.Json;

namespace Backend.Api.Tests.WorkflowEngine
{
    public sealed class Level1HttpWorkflowExecutionTests
    {
        [Fact]
        public async Task ExecuteAsync_ShouldRunLevel1ThroughGenericRunnerAndPersistBoundedOutput()
        {
            await using var db = WorkflowTestHelpers.CreateInMemoryDbContext();

            var workflow = new Workflow
            {
                ID = 1,
                UserID = 7,
                Name = "Weather",
                IsEnabled = true,
                Timezone = "America/Chicago"
            };

            var step = new WorkflowStep
            {
                ID = 1,
                WorkflowID = workflow.ID,
                StepKey = "fetch",
                StepType = Level1HttpStepExecutor.StepTypeName,
                StepOrder = 1,
                ConfigJson =
                    """
                    {
                      "originId":"test-origin",
                      "path":"/forecast",
                      "query":{
                        "latitude":"{{ workflow.inputs.latitude }}",
                        "timezone":"{{ execution.timezone }}"
                      },
                      "maximumResponseBytes":65536
                    }
                    """
            };

            db.Workflow.Add(workflow);
            db.WorkflowStep.Add(step);
            await db.SaveChangesAsync();

            var safeHttp = new Mock<ISafeOutboundHttpClient>();
            safeHttp
                .Setup(client => client.SendAsync(
                    It.Is<SafeHttpRequest>(request =>
                        request.UserId == 7 &&
                        request.WorkflowId == 1 &&
                        request.OriginId == "test-origin" &&
                        request.Query["latitude"] == "41.88" &&
                        request.Query["timezone"] == "America/Chicago"),
                    It.IsAny<OutboundRequestPolicy>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(
                    new SafeHttpResponse(
                        200,
                        "application/json",
                        JsonSerializer.SerializeToElement(new { current = new { temperature = 72 } }),
                        new Dictionary<string, string>
                        {
                            ["etag"] = "\"abc\""
                        },
                        42));

            var origins = new ApprovedHttpOriginsOptions();
            origins.Origins["test-origin"] = new ApprovedHttpOriginOptions
            {
                BaseUri = "https://api.example.com",
                AllowedMethods = ["GET"],
                AllowedPathPrefixes = ["/forecast"],
                AllowedQueryParameters = ["latitude", "timezone"],
                SelectedResponseHeaders = ["etag"],
                MaximumResponseBytes = 65536
            };

            var safeOptions = Options.Create(new SafeHttpOptions());
            var catalog = new ApprovedHttpOriginCatalog(Options.Create(origins), safeOptions);
            var parser = new WorkflowReferenceParser();
            var materializer = new Level1HttpRequestMaterializer(parser, new WorkflowReferenceResolver(), safeOptions);
            var executor = new Level1HttpStepExecutor(catalog, materializer, safeHttp.Object);
            var registry = new StepExecutorRegistry([executor]);
            var validation = new WorkflowValidationService(registry, parser);

            var clock = new FakeClock(DateTimeOffset.Parse("2026-01-01T01:00:00Z"));

            var runner = new WorkflowRunner(db, registry, clock, Options.Create(
                new WorkflowExecutionOptions
                {
                    StepTimeoutSeconds = 5,
                    WorkflowTimeoutSeconds = 30
                }));

            var service = new WorkflowExecutionService(
                db,
                WorkflowTestHelpers.CreateCurrentUserServiceMock(7).Object,
                validation,
                runner,
                new ExecutionDateResolver(clock, new TimezoneValidator()),
                clock);

            using var inputs = JsonDocument.Parse("""{"latitude":41.88}""");

            var result = await service.ExecuteAsync(workflow.ID, WorkflowTriggerTypes.Manual, inputs.RootElement, cancellationToken: CancellationToken.None);

            result.Succeeded.Should().BeTrue();
            result.Data.Should().NotBeNull();
            result.Data!.Status.Should().Be(ExecutionStatuses.Succeeded);

            var workflowExecution = db.WorkflowExecution.Single();
            workflowExecution.Status.Should().Be(ExecutionStatuses.Succeeded);
            workflowExecution.ErrorCode.Should().BeNull();

            var stepExecution = db.StepExecution.Single();
            stepExecution.Status.Should().Be(ExecutionStatuses.Succeeded);
            stepExecution.OutputJson.Should().NotBeNull();

            using var output = JsonDocument.Parse(stepExecution.OutputJson!);

            output.RootElement.GetProperty("body").GetProperty("current").GetProperty("temperature").GetInt32().Should().Be(72);
            output.RootElement.GetProperty("receivedBytes").GetInt32().Should().Be(42);
            output.RootElement.TryGetProperty("requestUri", out _).Should().BeFalse();
            output.RootElement.TryGetProperty("cookies", out _).Should().BeFalse();
            output.RootElement.TryGetProperty("connection", out _).Should().BeFalse();

            safeHttp.VerifyAll();
        }

        [Fact]
        public async Task ExecuteAsync_ShouldPersistOnlySanitizedLevel1Failure()
        {
            await using var db = WorkflowTestHelpers.CreateInMemoryDbContext();

            var workflow = new Workflow
            {
                ID = 1,
                UserID = 7,
                Name = "Failure",
                IsEnabled = true,
                Timezone = "UTC"
            };

            var step = new WorkflowStep
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
                      "path":"/forecast"
                    }
                    """
            };

            db.Workflow.Add(workflow);
            db.WorkflowStep.Add(step);
            await db.SaveChangesAsync();

            var safeHttp = new Mock<ISafeOutboundHttpClient>();
            safeHttp
                .Setup(client => client.SendAsync(It.IsAny<SafeHttpRequest>(), It.IsAny<OutboundRequestPolicy>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new SafeHttpException(ExecutionErrorCodes.HttpRequestFailed, "The outbound HTTP request failed."));

            var origins = new ApprovedHttpOriginsOptions();
            origins.Origins["test-origin"] = new ApprovedHttpOriginOptions
            {
                BaseUri = "https://api.example.com",
                AllowedMethods = ["GET"],
                AllowedPathPrefixes = ["/forecast"]
            };

            var safeOptions = Options.Create(new SafeHttpOptions());
            var catalog = new ApprovedHttpOriginCatalog(Options.Create(origins), safeOptions);
            var parser = new WorkflowReferenceParser();
            var executor = new Level1HttpStepExecutor(catalog, new Level1HttpRequestMaterializer(parser, new WorkflowReferenceResolver(), safeOptions), safeHttp.Object);
            var registry = new StepExecutorRegistry([executor]);
            var validation = new WorkflowValidationService(registry, parser);
            var clock = new FakeClock(DateTimeOffset.Parse("2026-01-01T01:00:00Z"));
            var runner = new WorkflowRunner(db, registry, clock, Options.Create(
                new WorkflowExecutionOptions
                {
                    StepTimeoutSeconds = 5,
                    WorkflowTimeoutSeconds = 30
                }));
            var service = new WorkflowExecutionService(
                db,
                WorkflowTestHelpers.CreateCurrentUserServiceMock(7).Object,
                validation,
                runner,
                new ExecutionDateResolver(clock, new TimezoneValidator()),
                clock);

            using var inputs = JsonDocument.Parse("{}");

            var result = await service.ExecuteAsync(1, WorkflowTriggerTypes.Manual, inputs.RootElement);

            result.Succeeded.Should().BeTrue();
            result.Data!.Status.Should().Be(ExecutionStatuses.Failed);
            result.Data.ErrorCode.Should().Be(ExecutionErrorCodes.HttpRequestFailed);

            var persistedStep = db.StepExecution.Single();
            persistedStep.ErrorCode.Should().Be(ExecutionErrorCodes.HttpRequestFailed);
            persistedStep.ErrorMessage.Should().Be("The outbound HTTP request failed.");
            persistedStep.OutputJson.Should().BeNull();

            var persistedWorkflow = db.WorkflowExecution.Single();
            persistedWorkflow.ErrorMessage.Should().Be("The outbound HTTP request failed.");
        }
    }
}
