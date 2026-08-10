using Backend.Api.Models.Entities;
using Backend.Api.Tests.Common;
using Backend.Api.WorkflowEngine.References;
using Backend.Api.WorkflowEngine.Registry;
using Backend.Api.WorkflowEngine.Validation;
using FluentAssertions;

namespace Backend.Api.Tests.WorkflowEngine
{
    public sealed class WorkflowValidationServiceTests
    {
        [Fact]
        public void Validate_ShouldRejectUnknownStepType()
        {
            var sut = CreateSut([]);

            var result = sut.Validate([ Step("one", "missing", 1, "{}") ], WorkflowValidationMode.Executable);

            result.Errors.Should().ContainSingle(x => x.Code == "workflow_step.type_unregistered");
        }

        [Fact]
        public void Validate_ShouldRejectForwardReference()
        {
            var executor = new FakeStepExecutor("fake");

            var sut = CreateSut([executor]);

            var result = sut.Validate([ Step("first", "fake", 1, """{"value":"{{ steps.second.output.value }}"}"""), Step("second", "fake", 2, "{}") ], WorkflowValidationMode.Executable);

            result.Errors.Should().Contain(x => x.Code == "workflow_step.reference_invalid");
        }

        [Fact]
        public void Validate_ShouldAcceptPriorKnownOutput()
        {
            var executor = new FakeStepExecutor("fake", new StepOutputSchema(new HashSet<string>(["value"], StringComparer.Ordinal)));

            var sut = CreateSut([executor]);

            var result = sut.Validate([ Step("first", "fake", 1, "{}"), Step("second", "fake", 2, """{"value":"{{ steps.first.output.value }}"}""") ], WorkflowValidationMode.Executable);

            result.Succeeded.Should().BeTrue();
        }

        [Fact]
        public void Validate_ShouldRejectDuplicateKeys()
        {
            var executor = new FakeStepExecutor("fake");

            var sut = CreateSut([executor]);

            var result = sut.Validate([ Step("same", "fake", 1, "{}"), Step("same", "fake", 2, "{}") ], WorkflowValidationMode.Executable);

            result.Errors.Should().Contain(x => x.Code == "workflow_step.key_duplicate");
        }

        private static WorkflowValidationService CreateSut(IEnumerable<FakeStepExecutor> executors) => new(new StepExecutorRegistry(executors), new WorkflowReferenceParser());

        private static WorkflowStep Step(string key, string type, int order, string config) => new()
        {
            WorkflowID = 1,
            StepKey = key,
            StepType = type,
            StepOrder = order,
            ConfigJson = config
        };
    }
}
