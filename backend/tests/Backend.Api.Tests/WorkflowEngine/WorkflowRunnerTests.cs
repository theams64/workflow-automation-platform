using Backend.Api.Configuration;
using Backend.Api.Models.Entities;
using Backend.Api.Tests.Common;
using Backend.Api.WorkflowEngine.Execution;
using Backend.Api.WorkflowEngine.Registry;
using FluentAssertions;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Backend.Api.Tests.WorkflowEngine
{
    public sealed class WorkflowRunnerTests
    {
        [Fact]
        public async Task RunAsync_ShouldExecuteInOrderAndPropagateOutputs()
        {
            await using var db = WorkflowTestHelpers.CreateInMemoryDbContext();
            var clock = new FakeClock(DateTimeOffset.Parse("2026-01-01T03:00:00Z"));
            var observed = new List<string>();

            var first = new FakeStepExecutor("first", execute: (_, _) =>
            {
                observed.Add("first");
                return Task.FromResult(StepExecutionResult.Success(NormalizedStepOutput.FromValue(new { value = 42 })));
            });

            var second = new FakeStepExecutor("second", execute: (context, _) =>
            {
                observed.Add("second");
                context.WorkflowContext.StepOutputs["first"].Value.GetProperty("value").GetInt32().Should().Be(42);

                return Task.FromResult(StepExecutionResult.Success());
            });

            var workflow = Workflow();
            var steps = new[]
            {
                Step(1, "first", "first"),
                Step(2, "second", "second")
            };
            var execution = Execution(workflow.ID, clock.UtcNow);

            db.Workflow.Add(workflow);
            db.WorkflowStep.AddRange(steps);
            db.WorkflowExecution.Add(execution);
            await db.SaveChangesAsync();

            var sut = new WorkflowRunner(db, new StepExecutorRegistry([first, second]), clock, Options.Create(new WorkflowExecutionOptions()));

            using var inputs = JsonDocument.Parse("{}");

            var result = await sut.RunAsync(new(workflow, steps, execution, inputs.RootElement), CancellationToken.None);

            result.Status.Should().Be(ExecutionStatuses.Succeeded);
            observed.Should().Equal("first", "second");
            db.StepExecution.Should().HaveCount(2);
        }

        [Fact]
        public async Task RunAsync_ShouldStopAfterFailure()
        {
            await using var db = WorkflowTestHelpers.CreateInMemoryDbContext();
            var clock = new FakeClock(DateTimeOffset.Parse("2026-01-01T03:00:00Z"));
            var laterExecuted = false;

            var failing = new FakeStepExecutor("fail", execute: (_, _) => Task.FromResult(StepExecutionResult.Failure("fake_failure", "Safe failure.")));

            var later = new FakeStepExecutor("later", execute: (_, _) =>
            {
                laterExecuted = true;
                return Task.FromResult(StepExecutionResult.Success());
            });

            var workflow = Workflow();
            var steps = new[]
            {
                Step(1, "first", "fail"),
                Step(2, "second", "later")
            };
            var execution = Execution(workflow.ID, clock.UtcNow);

            db.Workflow.Add(workflow);
            db.WorkflowStep.AddRange(steps);
            db.WorkflowExecution.Add(execution);
            await db.SaveChangesAsync();

            var sut = new WorkflowRunner(db, new StepExecutorRegistry([failing, later]), clock, Options.Create(new WorkflowExecutionOptions()));

            using var inputs = JsonDocument.Parse("{}");

            var result = await sut.RunAsync(new(workflow, steps, execution, inputs.RootElement), CancellationToken.None);

            result.Status.Should().Be(ExecutionStatuses.Failed);
            laterExecuted.Should().BeFalse();
            db.StepExecution.Should().ContainSingle();
        }

        [Fact]
        public async Task RunAsync_ShouldApplyStepTimeout()
        {
            await using var db = WorkflowTestHelpers.CreateInMemoryDbContext();
            var clock = new FakeClock(DateTimeOffset.Parse("2026-01-01T03:00:00Z"));

            var slow = new FakeStepExecutor("slow", execute: async (_, cancellationToken) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return StepExecutionResult.Success();
            });

            var workflow = Workflow();
            var steps = new[] { Step(1, "slow", "slow") };
            var execution = Execution(workflow.ID, clock.UtcNow);

            db.Workflow.Add(workflow);
            db.WorkflowStep.AddRange(steps);
            db.WorkflowExecution.Add(execution);
            await db.SaveChangesAsync();

            var sut = new WorkflowRunner(db, new StepExecutorRegistry([slow]), clock, Options.Create(new WorkflowExecutionOptions
            {
                StepTimeoutSeconds = 1,
                WorkflowTimeoutSeconds = 10
            }));

            using var inputs = JsonDocument.Parse("{}");

            var result = await sut.RunAsync(new(workflow, steps, execution, inputs.RootElement), CancellationToken.None);

            result.Status.Should().Be(ExecutionStatuses.Failed);
            result.ErrorCode.Should().Be(ExecutionErrorCodes.StepTimeout);
        }

        private static Workflow Workflow() => new()
        {
            ID = 1,
            UserID = 7,
            Name = "Test",
            IsEnabled = true,
            Timezone = "UTC"
        };

        private static WorkflowStep Step(int order, string key, string type) => new()
        {
            ID = order,
            WorkflowID = 1,
            StepKey = key,
            StepType = type,
            StepOrder = order,
            ConfigJson = "{}"
        };

        private static WorkflowExecution Execution(int workflowId, DateTimeOffset now) => new()
        {
            ID = Guid.NewGuid(),
            WorkflowID = workflowId,
            InitiatingUserID = 7,
            TriggerType = WorkflowTriggerTypes.Manual,
            Status = ExecutionStatuses.Pending,
            CreatedAt = now,
            EffectiveDate = new DateOnly(2026, 7, 29),
            Timezone = "UTC",
            InputJson = "{}"
        };
    }
}
