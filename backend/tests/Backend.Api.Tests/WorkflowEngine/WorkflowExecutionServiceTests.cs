using Backend.Api.Models.Entities;
using Backend.Api.Services.WorkflowExecution;
using Backend.Api.Tests.Common;
using Backend.Api.WorkflowEngine.Abstractions;
using Backend.Api.WorkflowEngine.Execution;
using Backend.Api.WorkflowEngine.Time;
using Backend.Api.WorkflowEngine.Validation;
using FluentAssertions;
using Moq;
using System.Text.Json;

namespace Backend.Api.Tests.WorkflowEngine
{
    public sealed class WorkflowExecutionServiceTests
    {
        [Fact]
        public async Task ExecuteAsync_ShouldReturnNotFound_ForOtherUsersWorkflow()
        {
            await using var db = WorkflowTestHelpers.CreateInMemoryDbContext();

            db.Workflow.Add(new Workflow
            {
                UserID = 99,
                Name = "Private",
                IsEnabled = true,
                Timezone = "UTC"
            });
            await db.SaveChangesAsync();

            var validation = new Mock<IWorkflowValidationService>();
            var runner = new Mock<IWorkflowRunner>();
            var clock = new FakeClock(DateTimeOffset.Parse("2026-01-01T03:00:00Z"));

            var sut = new WorkflowExecutionService(db, WorkflowTestHelpers.CreateCurrentUserServiceMock(7).Object, validation.Object, runner.Object, new ExecutionDateResolver(clock, new TimezoneValidator()), clock);

            using var inputs = JsonDocument.Parse("{}");

            var result = await sut.ExecuteAsync(1, WorkflowTriggerTypes.Manual, inputs.RootElement);

            result.Succeeded.Should().BeFalse();
            result.Errors.Should().ContainSingle(x => x.Code == "workflow.not_found");
            runner.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task ExecuteAsync_ShouldValidateBeforeCreatingExecution()
        {
            await using var db = WorkflowTestHelpers.CreateInMemoryDbContext();

            db.Workflow.Add(new Workflow
            {
                ID = 1,
                UserID = 7,
                Name = "Invalid",
                IsEnabled = true,
                Timezone = "UTC"
            });
            await db.SaveChangesAsync();

            var validation = new Mock<IWorkflowValidationService>();
            validation
                .Setup(x => x.Validate(It.IsAny<IReadOnlyList<WorkflowStep>>(), WorkflowValidationMode.Executable))
                .Returns(new WorkflowValidationResult([ new("invalid_reference", "Invalid workflow.") ]));

            var runner = new Mock<IWorkflowRunner>();
            var clock = new FakeClock(DateTimeOffset.Parse("2026-01-01T03:00:00Z"));

            var sut = new WorkflowExecutionService(db, WorkflowTestHelpers.CreateCurrentUserServiceMock(7).Object, validation.Object, runner.Object, new ExecutionDateResolver(clock, new TimezoneValidator()), clock);

            using var inputs = JsonDocument.Parse("{}");

            var result = await sut.ExecuteAsync(1, WorkflowTriggerTypes.Manual, inputs.RootElement);

            result.Succeeded.Should().BeFalse();
            db.WorkflowExecution.Should().BeEmpty();
            runner.VerifyNoOtherCalls();
        }
    }
}
