using Backend.Api.WorkflowEngine.Execution;
using Backend.Api.WorkflowEngine.References;
using FluentAssertions;
using System.Text.Json;

namespace Backend.Api.Tests.WorkflowEngine
{
    public sealed class WorkflowReferenceResolverTests
    {
        private readonly WorkflowReferenceParser _parser = new();
        private readonly WorkflowReferenceResolver _resolver = new();

        [Fact]
        public void TryResolve_ShouldResolveWorkflowInputsAndPriorOutputs()
        {
            using var inputs = JsonDocument.Parse("""{"city":"Chicago"}""");

            var context = Context(inputs.RootElement);
            context.AddStepOutput("fetch", 
                NormalizedStepOutput.FromValue(new
                {
                    body = new
                    {
                        values = new[] { 10, 20 }
                    }
                }));

            _parser.TryParse("workflow.inputs.city", out var inputReference).Should().BeTrue();
            _parser.TryParse("steps.fetch.output.body.values.1", out var outputReference).Should().BeTrue();

            _resolver.TryResolve(inputReference, context, out var city).Should().BeTrue();
            _resolver.TryResolve(outputReference, context, out var value).Should().BeTrue();

            city.GetString().Should().Be("Chicago");
            value.GetInt32().Should().Be(20);
        }

        [Fact]
        public void TryResolve_ShouldResolveOnlyApprovedExecutionMetadata()
        {
            using var inputs = JsonDocument.Parse("{}");
            var context = Context(inputs.RootElement);

            _parser.TryParse("execution.timezone", out var reference).Should().BeTrue();

            _resolver.TryResolve(reference, context, out var value).Should().BeTrue();

            value.GetString().Should().Be("America/Chicago");
        }

        [Fact]
        public void TryResolve_ShouldNotResolveCurrentItemOutsideControlledIteration()
        {
            using var inputs = JsonDocument.Parse("{}");
            var context = Context(inputs.RootElement);

            _parser.TryParse("item.value", out var reference).Should().BeTrue();

            _resolver.TryResolve(reference, context, out _).Should().BeFalse();
        }

        private static WorkflowExecutionContext Context(JsonElement inputs) => new(
            new WorkflowExecutionMetadata(
                Guid.NewGuid(),
                WorkflowTriggerTypes.Manual,
                DateTimeOffset.Parse("2026-01-01T01:00:00Z"),
                null,
                new DateOnly(2026, 1, 1),
                "America/Chicago"),
            inputs);
    }
}
