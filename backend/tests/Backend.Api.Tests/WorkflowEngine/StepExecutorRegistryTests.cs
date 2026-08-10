using Backend.Api.Tests.Common;
using Backend.Api.WorkflowEngine.Registry;
using FluentAssertions;

namespace Backend.Api.Tests.WorkflowEngine
{
    public sealed class StepExecutorRegistryTests
    {
        [Fact]
        public void Constructor_ShouldRejectDuplicateTypes()
        {
            var action = () => new StepExecutorRegistry(
            [
                new FakeStepExecutor("fake"),
                new FakeStepExecutor("FAKE")
            ]);

            action.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void TryGet_ShouldResolveCaseInsensitively()
        {
            var executor = new FakeStepExecutor("fake");
            var registry = new StepExecutorRegistry([executor]);

            registry.TryGet("FAKE", out var resolved).Should().BeTrue();
            resolved.Should().BeSameAs(executor);
        }

        [Fact]
        public void TryGet_ShouldReturnFalse_ForUnknownType()
        {
            var registry = new StepExecutorRegistry([]);

            registry.TryGet("missing", out _).Should().BeFalse();
        }
    }
}
