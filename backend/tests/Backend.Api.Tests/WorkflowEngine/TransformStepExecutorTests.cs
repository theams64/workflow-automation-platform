using Backend.Api.Configuration;
using Backend.Api.Models.Entities;
using Backend.Api.WorkflowEngine.Execution;
using Backend.Api.WorkflowEngine.Expressions;
using Backend.Api.WorkflowEngine.References;
using Backend.Api.WorkflowEngine.Time;
using Backend.Api.WorkflowEngine.Transform;
using Backend.Api.WorkflowEngine.Validation;
using FluentAssertions;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Backend.Api.Tests.WorkflowEngine
{
    public sealed class TransformStepExecutorTests
    {
        [Fact]
        public async Task ExecuteAsync_ShouldSelectRenameMapFormatCountTruncateAndRound()
        {
            var sut = CreateExecutor();
            var context = CreateContext(
                """
                {
                  "location":"Chicago"
                }
                """,
                """
                {
                  "output": {
                    "location":"{{ workflow.inputs.location }}",
                    "eventCount":"{{ count(steps.fetch.output.body.events) }}",
                    "rounded":"{{ round(steps.fetch.output.body.score, 2) }}",
                    "events": {
                      "$map": {
                        "source":"{{ steps.fetch.output.body.events }}",
                        "maximumItems":10,
                        "item": {
                          "title":"{{ truncate(item.name, 20) }}",
                          "venue":"{{ default(item.venue.name, \"Venue unavailable\") }}",
                          "time":"{{ formatDateTime(item.start, \"h:mm tt\", execution.timezone) }}",
                          "priceText":"{{ priceText(item.price.minimum, item.price.currency) }}"
                        }
                      }
                    }
                  }
                }
                """);

            context.WorkflowContext.AddStepOutput("fetch", NormalizedStepOutput.FromValue(
                new
                {
                    body = new
                    {
                        score = 1.236m,
                        events = new object[]
                        {
                            new
                            {
                                name = "Example Concert With A Long Name",
                                start = "2026-01-01T19:30:00-05:00",
                                venue = new { name = (string?)null },
                                price = new { minimum = 42.5m, currency = "USD" }
                            }
                        }
                    }
                }));

            var result = await sut.ExecuteAsync(context, CancellationToken.None);

            result.Succeeded.Should().BeTrue();
            result.Output.Should().NotBeNull();

            var output = result.Output!.Value;
            output.GetProperty("location").GetString().Should().Be("Chicago");
            output.GetProperty("eventCount").GetInt32().Should().Be(1);
            output.GetProperty("rounded").GetDecimal().Should().Be(1.24m);

            var item = output.GetProperty("events")[0];
            item.GetProperty("title").GetString().Should().Be("Example Concert With");
            item.GetProperty("venue").GetString().Should().Be("Venue unavailable");
            item.GetProperty("time").GetString().Should().NotBeNullOrWhiteSpace();
            item.GetProperty("priceText").GetString().Should().Be("From $42.50");
        }

        [Fact]
        public async Task ExecuteAsync_ShouldKeepTemplateLikeExternalDataLiteral()
        {
            var sut = CreateExecutor();
            var context = CreateContext(
                "{}",
                """
                {
                  "output": {
                    "value":"{{ steps.fetch.output.body.value }}"
                  }
                }
                """);

            context.WorkflowContext.AddStepOutput("fetch", NormalizedStepOutput.FromValue(
                new
                {
                    body = new
                    {
                        value = "{{ environment.API_KEY }}"
                    }
                }));

            var result = await sut.ExecuteAsync(context, CancellationToken.None);

            result.Succeeded.Should().BeTrue();
            result.Output!.Value.GetProperty("value").GetString().Should().Be("{{ environment.API_KEY }}");
        }

        [Fact]
        public void ValidateConfiguration_ShouldRejectItemReferenceOutsideMap()
        {
            var sut = CreateExecutor();

            var result = sut.ValidateConfiguration(
                """
                {
                  "output": {
                    "value":"{{ item.name }}"
                  }
                }
                """,
                new WorkflowValidationContext(
                    1,
                    new Dictionary<string, StepOutputSchema>()));

            result.Should().Contain(error =>
                error.Code == "workflow_step.transform_expression_invalid");
        }

        [Fact]
        public void ValidateConfiguration_ShouldRejectNestedMaps()
        {
            var sut = CreateExecutor();
            var prior = PriorSchemas();

            var result = sut.ValidateConfiguration(
                """
                {
                  "output": {
                    "groups": {
                      "$map": {
                        "source":"{{ steps.fetch.output.body.groups }}",
                        "maximumItems":10,
                        "item": {
                          "values": {
                            "$map": {
                              "source":"{{ item.values }}",
                              "maximumItems":10,
                              "item":"{{ item.value }}"
                            }
                          }
                        }
                      }
                    }
                  }
                }
                """,
                new WorkflowValidationContext(2, prior));

            result.Should().Contain(error =>
                error.Code == "workflow_step.transform_nested_map_invalid");
        }

        [Fact]
        public void GetOutputSchema_ShouldDeriveConfiguredFields()
        {
            var sut = CreateExecutor();

            var schema = sut.GetOutputSchema(
                """
                {
                  "output": {
                    "location":"Chicago",
                    "events": {
                      "$map": {
                        "source":"{{ steps.fetch.output.body.events }}",
                        "maximumItems":10,
                        "item": {
                          "title":"{{ item.name }}",
                          "venue":"{{ item.venue.name }}"
                        }
                      }
                    }
                  }
                }
                """);

            schema.Supports("location").Should().BeTrue();
            schema.Supports("events").Should().BeTrue();
            schema.Supports("events.title").Should().BeTrue();
            schema.Supports("events.venue").Should().BeTrue();
            schema.Supports("events.secret").Should().BeFalse();
        }

        private static TransformStepExecutor CreateExecutor()
        {
            var expressionOptions = Options.Create(
                new WorkflowExpressionOptions());
            var expressions = new WorkflowExpressionEngine(
                new WorkflowReferenceParser(),
                new WorkflowReferenceResolver(),
                new TimezoneValidator(),
                expressionOptions);
            var processor = new TransformTemplateProcessor(
                expressions,
                expressionOptions,
                Options.Create(new TransformOptions()));

            return new TransformStepExecutor(processor);
        }

        private static StepExecutionContext CreateContext(
            string workflowInputs,
            string configJson)
        {
            using var document = JsonDocument.Parse(workflowInputs);

            var workflowContext = new WorkflowExecutionContext(
                new WorkflowExecutionMetadata(
                    Guid.NewGuid(),
                    WorkflowTriggerTypes.Manual,
                    DateTimeOffset.Parse("2026-08-16T12:00:00Z"),
                    null,
                    new DateOnly(2026, 8, 16),
                    "America/Chicago"),
                document.RootElement);

            return new StepExecutionContext(
                new Workflow
                {
                    ID = 1,
                    UserID = 7,
                    Name = "Transform",
                    IsEnabled = true,
                    Timezone = "America/Chicago"
                },
                new WorkflowStep
                {
                    ID = 2,
                    WorkflowID = 1,
                    StepKey = "transform",
                    StepType = TransformStepExecutor.StepTypeName,
                    StepOrder = 2,
                    ConfigJson = configJson
                },
                workflowContext);
        }

        private static IReadOnlyDictionary<string, StepOutputSchema> PriorSchemas() =>
            new Dictionary<string, StepOutputSchema>
            {
                ["fetch"] = new StepOutputSchema(
                    new HashSet<string>(StringComparer.Ordinal),
                    AllowAdditionalPaths: true)
            };
    }
}