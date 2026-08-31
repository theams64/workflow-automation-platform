using Backend.Api.Configuration;
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
    public sealed class WorkflowExpressionEngineTests
    {
        [Fact]
        public void ValidateTemplate_ShouldRejectUnknownOrDangerousRoots()
        {
            var sut = CreateSut();
            var context = new WorkflowValidationContext(1, new Dictionary<string, StepOutputSchema>());

            var result = sut.ValidateTemplate("{{ environment.API_KEY }}", context, allowItemReferences: false);

            result.Should().ContainSingle();
        }

        [Fact]
        public void ValidateTemplate_ShouldRejectItemOutsideControlledIteration()
        {
            var sut = CreateSut();
            var context = new WorkflowValidationContext(1, new Dictionary<string, StepOutputSchema>());

            var result = sut.ValidateTemplate("{{ item.name }}", context, allowItemReferences: false);

            result.Should().ContainSingle();
        }

        [Fact]
        public void ValidateTemplate_ShouldAcceptNestedAllowedHelpers()
        {
            var sut = CreateSut();
            var prior = new Dictionary<string, StepOutputSchema>
            {
                ["fetch"] = new StepOutputSchema(new HashSet<string>(StringComparer.Ordinal), AllowAdditionalPaths: true)
            };

            var result = sut.ValidateTemplate("{{ round(toNumber(steps.fetch.output.body.price), 2) }}", new WorkflowValidationContext(2, prior), allowItemReferences: false);

            result.Should().BeEmpty();
        }

        [Fact]
        public void EvaluateTemplate_ShouldUseDefaultForMissingReference()
        {
            var sut = CreateSut();
            var context = ExecutionContext("{}");
            var budget = new ExpressionBudget(100, CancellationToken.None);

            var result = sut.EvaluateTemplate("{{ default(workflow.inputs.missing, \"fallback\") }}", new ExpressionEvaluationContext(context), budget);

            result.GetString().Should().Be("fallback");
        }

        [Fact]
        public void EvaluateTemplate_ShouldNotRecursivelyEvaluateResolvedStrings()
        {
            var sut = CreateSut();
            var context = ExecutionContext("{}");
            context.AddStepOutput("prior", 
                NormalizedStepOutput.FromValue(new
                {
                    value = "{{ execution.timezone }}"
                }));

            var result = sut.EvaluateTemplate("{{ steps.prior.output.value }}", new ExpressionEvaluationContext(context), new ExpressionBudget(100, CancellationToken.None));

            result.GetString().Should().Be("{{ execution.timezone }}");
        }

        [Fact]
        public void EvaluateTemplate_ShouldEnforceOperationBudget()
        {
            var sut = CreateSut();
            var context = ExecutionContext("{}");

            var action = () => sut.EvaluateTemplate("{{ uppercase(lowercase(\"value\")) }}", new ExpressionEvaluationContext(context), new ExpressionBudget(1, CancellationToken.None));

            action.Should().Throw<WorkflowExpressionException>();
        }

        private static WorkflowExpressionEngine CreateSut() => new(new WorkflowReferenceParser(), new WorkflowReferenceResolver(), new TimezoneValidator(), Options.Create(new WorkflowExpressionOptions()));

        private static WorkflowExecutionContext ExecutionContext(string inputs)
        {
            using var document = JsonDocument.Parse(inputs);

            return new WorkflowExecutionContext(
                new WorkflowExecutionMetadata(
                    Guid.NewGuid(),
                    WorkflowTriggerTypes.Manual,
                    DateTimeOffset.Parse("2026-01-01T12:00:00Z"),
                    null,
                    new DateOnly(2026, 1, 1),
                    "America/Chicago"),
                document.RootElement);
        }
    }
}