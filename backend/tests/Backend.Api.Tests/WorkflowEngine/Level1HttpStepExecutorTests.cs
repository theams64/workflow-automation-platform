using Backend.Api.Configuration;
using Backend.Api.Models.Entities;
using Backend.Api.Tests.Common;
using Backend.Api.WorkflowEngine.Execution;
using Backend.Api.WorkflowEngine.Http;
using Backend.Api.WorkflowEngine.Http.Level1;
using Backend.Api.WorkflowEngine.References;
using Backend.Api.WorkflowEngine.Validation;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using System.Text.Json;

namespace Backend.Api.Tests.WorkflowEngine
{
    public sealed class Level1HttpStepExecutorTests
    {
        [Fact]
        public void ValidateConfiguration_ShouldRejectUnknownOrigin()
        {
            var sut = CreateExecutor(CreateCatalog(includeOrigin: false), new Mock<ISafeOutboundHttpClient>().Object);

            var result = sut.ValidateConfiguration("""{"originId":"missing","path":"/forecast"}""", new WorkflowValidationContext(1, new Dictionary<string, StepOutputSchema>()));

            result.Should().ContainSingle(error => error.Code == "workflow_step.http_origin_invalid");
        }

        [Fact]
        public void ValidateConfiguration_ShouldRejectDisabledOrigin()
        {
            var sut = CreateExecutor(CreateCatalog(enabled: false), new Mock<ISafeOutboundHttpClient>().Object);

            var result = sut.ValidateConfiguration("""{"originId":"test-origin","path":"/forecast"}""", new WorkflowValidationContext(1, new Dictionary<string, StepOutputSchema>()));

            result.Should().ContainSingle(error => error.Code == "workflow_step.http_origin_disabled");
        }

        [Theory]
        [InlineData("https://evil.example/path")]
        [InlineData("//evil.example/path")]
        [InlineData("/forecast/%2e%2e/admin")]
        [InlineData("/forecast/../admin")]
        [InlineData("/forecast\\admin")]
        [InlineData("/forecast#x")]
        public void ValidateConfiguration_ShouldRejectUnsafePath(string path)
        {
            var sut = CreateExecutor(CreateCatalog(), new Mock<ISafeOutboundHttpClient>().Object);

            var config = JsonSerializer.Serialize(new
            {
                originId = "test-origin",
                path
            });

            var result = sut.ValidateConfiguration(config, new WorkflowValidationContext(1, new Dictionary<string, StepOutputSchema>()));

            result.Should().Contain(error => error.Code == "workflow_step.http_path_invalid");
        }

        [Fact]
        public void ValidateConfiguration_ShouldRejectPathOutsideApprovedPrefix()
        {
            var sut = CreateExecutor(CreateCatalog(), new Mock<ISafeOutboundHttpClient>().Object);

            var result = sut.ValidateConfiguration("""{"originId":"test-origin","path":"/admin"}""", new WorkflowValidationContext(1, new Dictionary<string, StepOutputSchema>()));

            result.Should().ContainSingle(error => error.Code == "workflow_step.http_path_not_allowed");
        }

        [Fact]
        public void ValidateConfiguration_ShouldRejectUnapprovedQueryParameter()
        {
            var sut = CreateExecutor(CreateCatalog(), new Mock<ISafeOutboundHttpClient>().Object);

            var result = sut.ValidateConfiguration(
                """
                {
                  "originId":"test-origin",
                  "path":"/forecast",
                  "query":{"admin":"true"}
                }
                """,
                new WorkflowValidationContext(1, new Dictionary<string, StepOutputSchema>()));

            result.Should().ContainSingle(error => error.Code == "workflow_step.http_query_not_allowed");
        }

        [Fact]
        public void ValidateConfiguration_ShouldRejectPartialReference()
        {
            var sut = CreateExecutor(CreateCatalog(), new Mock<ISafeOutboundHttpClient>().Object);

            var result = sut.ValidateConfiguration(
                """
                {
                  "originId":"test-origin",
                  "path":"/forecast",
                  "query":{"q":"prefix-{{ workflow.inputs.value }}"}
                }
                """,
                new WorkflowValidationContext(1, new Dictionary<string, StepOutputSchema>()));

            result.Should().ContainSingle(error => error.Code == "workflow_step.http_template_invalid");
        }

        [Fact]
        public void ValidateConfiguration_ShouldRejectItemReference()
        {
            var sut = CreateExecutor(CreateCatalog(), new Mock<ISafeOutboundHttpClient>().Object);

            var result = sut.ValidateConfiguration(
                """
                {
                  "originId":"test-origin",
                  "path":"/forecast",
                  "query":{"q":"{{ item.value }}"}
                }
                """,
                new WorkflowValidationContext(1, new Dictionary<string, StepOutputSchema>()));

            result.Should().ContainSingle(error => error.Code == "workflow_step.reference_invalid");
        }

        [Theory]
        [InlineData("method", "\"POST\"")]
        [InlineData("headers", "{\"Authorization\":\"secret\"}")]
        [InlineData("body", "{\"value\":1}")]
        [InlineData("url", "\"https://evil.example/\"")]
        [InlineData("connectionId", "\"not-level-1\"")]
        public void ValidateConfiguration_ShouldRejectUnsupportedConfigurationFields(string propertyName, string propertyJson)
        {
            var sut = CreateExecutor(CreateCatalog(), new Mock<ISafeOutboundHttpClient>().Object);

            var config =
                $$"""
                {
                  "originId":"test-origin",
                  "path":"/forecast",
                  "{{propertyName}}":{{propertyJson}}
                }
                """;

            var result = sut.ValidateConfiguration(config, new WorkflowValidationContext(1, new Dictionary<string, StepOutputSchema>()));

            result.Should().ContainSingle(error => error.Code == "workflow_step.config_invalid");
        }

        [Fact]
        public void ValidateConfiguration_ShouldRejectResponseLimitAboveOriginPolicy()
        {
            var sut = CreateExecutor(CreateCatalog(), new Mock<ISafeOutboundHttpClient>().Object);

            var result = sut.ValidateConfiguration(
                """
                {
                  "originId":"test-origin",
                  "path":"/forecast",
                  "maximumResponseBytes":65537
                }
                """,
                new WorkflowValidationContext(1, new Dictionary<string, StepOutputSchema>()));

            result.Should().ContainSingle(error => error.Code == "workflow_step.http_response_limit_invalid");
        }

        [Fact]
        public async Task ExecuteAsync_ShouldResolveScalarReferencesAndNormalizeResponse()
        {
            var safeClient = new Mock<ISafeOutboundHttpClient>();
            safeClient
                .Setup(client => client.SendAsync(
                    It.Is<SafeHttpRequest>(request => 
                        request.Method == HttpMethod.Get && 
                        request.Query["q"] == "41.88" && 
                        request.Path == "/forecast/chicago"),
                    It.IsAny<OutboundRequestPolicy>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(
                    new SafeHttpResponse(
                        200,
                        "application/json",
                        JsonSerializer.SerializeToElement(new { temperature = 72 }),
                        new Dictionary<string, string>(),
                        18));

            var sut = CreateExecutor(CreateCatalog(), safeClient.Object);

            var context = CreateExecutionContext(
                """{"latitude":41.88,"city":"chicago"}""",
                """
                {
                  "originId":"test-origin",
                  "path":"/forecast/{{ workflow.inputs.city }}",
                  "query":{"q":"{{ workflow.inputs.latitude }}"}
                }
                """);

            var result = await sut.ExecuteAsync(context, CancellationToken.None);

            result.Succeeded.Should().BeTrue();
            result.Output.Should().NotBeNull();
            result.Output!.Value.GetProperty("body").GetProperty("temperature").GetInt32().Should().Be(72);
        }

        [Fact]
        public async Task ExecuteAsync_ShouldRejectTraversalResolvedFromReference()
        {
            var safeClient = new Mock<ISafeOutboundHttpClient>();
            var sut = CreateExecutor(CreateCatalog(), safeClient.Object);

            var context = CreateExecutionContext(
                """{"segment":".."}""",
                """
                {
                  "originId":"test-origin",
                  "path":"/forecast/{{ workflow.inputs.segment }}"
                }
                """);

            var result = await sut.ExecuteAsync(context, CancellationToken.None);

            result.Succeeded.Should().BeFalse();
            result.ErrorCode.Should().Be(ExecutionErrorCodes.RequestUriInvalid);
            safeClient.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task ExecuteAsync_ShouldReturnInvalidReferenceForMissingValue()
        {
            var safeClient = new Mock<ISafeOutboundHttpClient>();
            var sut = CreateExecutor(CreateCatalog(), safeClient.Object);

            var context = CreateExecutionContext(
                "{}",
                """
                {
                  "originId":"test-origin",
                  "path":"/forecast",
                  "query":{"q":"{{ workflow.inputs.missing }}"}
                }
                """);

            var result = await sut.ExecuteAsync(context, CancellationToken.None);

            result.Succeeded.Should().BeFalse();
            result.ErrorCode.Should().Be(ExecutionErrorCodes.InvalidReference);
            safeClient.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task ExecuteAsync_ShouldNotRecursivelyEvaluateResolvedStrings()
        {
            var safeClient = new Mock<ISafeOutboundHttpClient>();
            safeClient
                .Setup(client => client.SendAsync(
                    It.Is<SafeHttpRequest>(request => request.Query["q"] == "{{ execution.timezone }}"),
                    It.IsAny<OutboundRequestPolicy>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(
                    new SafeHttpResponse(
                        200,
                        "application/json",
                        JsonSerializer.SerializeToElement(new { ok = true }),
                        new Dictionary<string, string>(),
                        11));

            var sut = CreateExecutor(CreateCatalog(), safeClient.Object);

            var context = CreateExecutionContext("{}", 
                """
                {
                    "originId":"test-origin",
                    "path":"/forecast",
                    "query":{
                    "q":"{{ steps.prior.output.body.value }}"
                    }
                }
                """);

            context.WorkflowContext.AddStepOutput("prior",
                NormalizedStepOutput.FromValue(new
                {
                    body = new
                    {
                        value = "{{ execution.timezone }}"
                    }
                }));

            var result = await sut.ExecuteAsync(context, CancellationToken.None);

            result.Succeeded.Should().BeTrue();
            safeClient.VerifyAll();
        }

        [Fact]
        public void OutputSchema_ShouldExposeEnvelopeAndBodyDescendantsOnly()
        {
            var sut = CreateExecutor(CreateCatalog(), new Mock<ISafeOutboundHttpClient>().Object);

            sut.OutputSchema.Supports("statusCode").Should().BeTrue();
            sut.OutputSchema.Supports("body.forecast.temperature").Should().BeTrue();
            sut.OutputSchema.Supports("selectedHeaders.etag").Should().BeTrue();
            sut.OutputSchema.Supports("transport.socket").Should().BeFalse();
        }

        private static Level1HttpStepExecutor CreateExecutor(IApprovedHttpOriginCatalog catalog, ISafeOutboundHttpClient safeClient)
        {
            var options = Options.Create(new SafeHttpOptions());
            var materializer = new Level1HttpRequestMaterializer(new WorkflowReferenceParser(), new WorkflowReferenceResolver(), options);

            return new Level1HttpStepExecutor(catalog, materializer, safeClient);
        }

        private static IApprovedHttpOriginCatalog CreateCatalog(bool includeOrigin = true, bool enabled = true)
        {
            var origins = new ApprovedHttpOriginsOptions();

            if (includeOrigin)
            {
                origins.Origins["test-origin"] = new ApprovedHttpOriginOptions
                {
                    Enabled = enabled,
                    BaseUri = "https://api.example.com",
                    AllowedMethods = ["GET"],
                    AllowedPathPrefixes = ["/forecast"],
                    AllowedQueryParameters = ["q"]
                };
            }

            return new ApprovedHttpOriginCatalog(Options.Create(origins), Options.Create(new SafeHttpOptions()));
        }

        private static StepExecutionContext CreateExecutionContext(string workflowInputs, string configJson)
        {
            using var inputs = JsonDocument.Parse(workflowInputs);

            var workflowContext = new WorkflowExecutionContext(
                new WorkflowExecutionMetadata(
                    Guid.NewGuid(),
                    WorkflowTriggerTypes.Manual,
                    DateTimeOffset.Parse("2026-08-09T12:00:00Z"),
                    null,
                    new DateOnly(2026, 8, 9),
                    "America/Chicago"),
                inputs.RootElement);

            var workflow = new Workflow
            {
                ID = 11,
                UserID = 7,
                Name = "HTTP Test",
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
                ConfigJson = configJson
            };

            return new StepExecutionContext(workflow, step, workflowContext);
        }
    }
}
