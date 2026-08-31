using Backend.Api.Configuration;
using Backend.Api.Models.Entities;
using Backend.Api.WorkflowEngine.Composition;
using Backend.Api.WorkflowEngine.Execution;
using Backend.Api.WorkflowEngine.Expressions;
using Backend.Api.WorkflowEngine.References;
using Backend.Api.WorkflowEngine.Time;
using Backend.Api.WorkflowEngine.Validation;
using FluentAssertions;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Backend.Api.Tests.WorkflowEngine
{
    public sealed class MessageCompositionStepExecutorTests
    {
        [Fact]
        public async Task ExecuteAsync_ShouldRenderSimpleTemplateFromWorkflowInputs()
        {
            var sut = CreateExecutor();
            var context = CreateContext(
                """
                {
                  "template":"Hello {{ workflow.inputs.name }}!"
                }
                """,
                """{"name":"Ada"}""");

            var result = await sut.ExecuteAsync(context, CancellationToken.None);

            result.Succeeded.Should().BeTrue();
            result.Output!.Value.GetProperty("text").GetString().Should().Be("Hello Ada!");
        }

        [Fact]
        public async Task ExecuteAsync_ShouldRenderSimpleLiteralWithoutPriorSteps()
        {
            var sut = CreateExecutor();
            var context = CreateContext(
                """
                {
                  "template":"Deployment completed successfully."
                }
                """);

            var result = await sut.ExecuteAsync(context, CancellationToken.None);

            result.Succeeded.Should().BeTrue();
            result.Output!.Value.GetProperty("text").GetString().Should().Be("Deployment completed successfully.");
        }

        [Fact]
        public async Task ExecuteAsync_ShouldRenderTitleAndBoundedItems()
        {
            var sut = CreateExecutor();
            var context = CreateContext(
                """
                {
                  "titleTemplate":"Items in {{ steps.transform.output.location }}",
                  "emptyMessageTemplate":"No Items.",
                  "collectionReference":"{{ steps.transform.output.items }}",
                  "itemTemplate":"- {{ item.title }} at {{ item.venue }}",
                  "maximumItems":1,
                  "separator":"\n"
                }
                """);

            context.WorkflowContext.AddStepOutput("transform", NormalizedStepOutput.FromValue(
                new
                {
                    location = "Chicago",
                    items = new[]
                    {
                        new { title = "One", venue = "A" },
                        new { title = "Two", venue = "B" }
                    }
                }));

            var result = await sut.ExecuteAsync(context, CancellationToken.None);

            result.Succeeded.Should().BeTrue();
            result.Output!.Value.GetProperty("text").GetString().Should().Be("Items in Chicago\n\n- One at A");
        }

        [Fact]
        public async Task ExecuteAsync_ShouldMapCollectionDirectlyFromWorkflowInputs()
        {
            var sut = CreateExecutor();
            var context = CreateContext(
                """
                {
                  "collectionReference":"{{ workflow.inputs.items }}",
                  "itemTemplate":"{{ item.name }}",
                  "maximumItems":10,
                  "separator":", "
                }
                """,
                """{"items":[{"name":"one"},{"name":"two"}]}""");

            var result = await sut.ExecuteAsync(context, CancellationToken.None);

            result.Succeeded.Should().BeTrue();
            result.Output!.Value.GetProperty("text").GetString().Should().Be("one, two");
        }

        [Fact]
        public async Task ExecuteAsync_ShouldUseEmptyTemplate()
        {
            var sut = CreateExecutor();
            var context = CreateContext(
                """
                {
                  "titleTemplate":"Items",
                  "emptyMessageTemplate":"No items were found.",
                  "collectionReference":"{{ steps.transform.output.items }}",
                  "itemTemplate":"{{ item.title }}",
                  "maximumItems":10
                }
                """);

            context.WorkflowContext.AddStepOutput("transform", NormalizedStepOutput.FromValue(new { items = Array.Empty<object>() }));

            var result = await sut.ExecuteAsync(context, CancellationToken.None);

            result.Output!.Value.GetProperty("text").GetString().Should().Be("Items\n\nNo items were found.");
        }

        [Fact]
        public void ValidateConfiguration_ShouldRejectItemTemplateWithoutCollection()
        {
            var sut = CreateExecutor();

            var result = sut.ValidateConfiguration(
                """
                {
                  "template":"Hello",
                  "itemTemplate":"{{ item.name }}"
                }
                """,
                new WorkflowValidationContext(1, new Dictionary<string, StepOutputSchema>()));

            result.Should().Contain(error => error.Code == "workflow_step.message_collection_invalid");
        }

        [Fact]
        public void ValidateConfiguration_ShouldRejectUnknownFunction()
        {
            var sut = CreateExecutor();

            var result = sut.ValidateConfiguration(
                """
                {
                  "template":"{{ execute(workflow.inputs.name) }}"
                }
                """,
                new WorkflowValidationContext(1, new Dictionary<string, StepOutputSchema>()));

            result.Should().Contain(error => error.Code == "workflow_step.message_expression_invalid");
        }

        [Fact]
        public async Task ExecuteAsync_ShouldRejectOversizedFinalMessage()
        {
            var expressionOptions = Options.Create(
                new WorkflowExpressionOptions
                {
                    MaxStringBytes = 1024
                });

            var expressions = new WorkflowExpressionEngine(new WorkflowReferenceParser(), new WorkflowReferenceResolver(), new TimezoneValidator(), expressionOptions);

            var sut = new MessageCompositionStepExecutor(
                expressions, 
                expressionOptions, 
                Options.Create(
                    new MessageCompositionOptions
                    {
                        MaxItems = 25,
                        MaxMessageBytes = 128,
                        MaxSeparatorBytes = 64
                    }));

            var context = CreateContext(JsonSerializer.Serialize(new { template = new string('x', 200) }));

            var result = await sut.ExecuteAsync(context, CancellationToken.None);

            result.Succeeded.Should().BeFalse();
            result.ErrorCode.Should().Be(ExecutionErrorCodes.MessageSizeExceeded);
        }

        private static MessageCompositionStepExecutor CreateExecutor()
        {
            var expressionOptions = Options.Create(new WorkflowExpressionOptions());
            var expressions = new WorkflowExpressionEngine(new WorkflowReferenceParser(), new WorkflowReferenceResolver(), new TimezoneValidator(), expressionOptions);

            return new MessageCompositionStepExecutor(expressions, expressionOptions, Options.Create(new MessageCompositionOptions()));
        }

        private static StepExecutionContext CreateContext(string configJson, string workflowInputs = "{}")
        {
            using var document = JsonDocument.Parse(workflowInputs);

            return new StepExecutionContext(
                new Workflow
                {
                    ID = 1,
                    UserID = 7,
                    Name = "Compose",
                    IsEnabled = true,
                    Timezone = "UTC"
                },
                new WorkflowStep
                {
                    ID = 3,
                    WorkflowID = 1,
                    StepKey = "compose",
                    StepType = MessageCompositionStepExecutor.StepTypeName,
                    StepOrder = 1,
                    ConfigJson = configJson
                },
                new WorkflowExecutionContext(
                    new WorkflowExecutionMetadata(
                        Guid.NewGuid(),
                        WorkflowTriggerTypes.Manual,
                        DateTimeOffset.Parse("2026-01-01T12:00:00Z"),
                        null,
                        new DateOnly(2026, 1, 1),
                        "UTC"),
                    document.RootElement));
        }
    }
}