using Backend.Api.Models.Entities;
using Backend.Api.Models.Dtos.Workflow;
using Backend.Api.Models.Dtos.WorkflowStep;
using Backend.Api.Services.Workflow;
using Backend.Api.Tests.Common;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Backend.Api.Services.Common;

namespace Backend.Api.Tests.Services
{
    public sealed class WorkflowServiceTests
    {
        [Fact]
        public async Task CreateWorkflowAsync_ShouldReturnUnauthorized_WhenCurrentUserCannotBeDetermined()
        {
            await using var db = WorkflowTestHelpers.CreateInMemoryDbContext();

            var currentUserService = new Mock<ICurrentUserService>();
            currentUserService
                .Setup(x => x.GetUserId())
                .Throws(new UnauthorizedAccessException("No User"));

            var cronValidator = WorkflowTestHelpers.CreateCronValidatorMock(true);
            var jsonValidator = WorkflowTestHelpers.CreateJsonValidatorMock(true);

            var sut = new WorkflowService(
                db,
                currentUserService.Object,
                cronValidator.Object,
                jsonValidator.Object);

            var request = new CreateWorkflowRequestDto
            {
                Name = "Test Workflow",
                IsEnabled = true
            };

            var result = await sut.CreateWorkflowAsync(request);

            result.Succeeded.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.Code == "auth.unauthorized");
        }

        [Fact]
        public async Task CreateWorkflowAsync_ShouldFail_WhenNameIsMissing()
        {
            await using var db = WorkflowTestHelpers.CreateInMemoryDbContext();

            var currentUserService = WorkflowTestHelpers.CreateCurrentUserServiceMock(7);
            var cronValidator = WorkflowTestHelpers.CreateCronValidatorMock(true);
            var jsonValidator = WorkflowTestHelpers.CreateJsonValidatorMock(true);

            var sut = new WorkflowService(
                db,
                currentUserService.Object,
                cronValidator.Object,
                jsonValidator.Object);

            var request = new CreateWorkflowRequestDto
            {
                Name = "   ",
                IsEnabled = true,
                TriggerType = "Scheduled",
                CronExpression = "0 0 * * *"
            };

            var result = await sut.CreateWorkflowAsync(request);

            result.Succeeded.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.Code == "workflow.name_required");
            db.Workflow.Should().BeEmpty();
        }

        [Fact]
        public async Task CreateWorkflowAsync_ShouldFail_WhenCronExpressionIsInvalid()
        {
            await using var db = WorkflowTestHelpers.CreateInMemoryDbContext();

            var currentUserService = WorkflowTestHelpers.CreateCurrentUserServiceMock(7);
            var cronValidator = WorkflowTestHelpers.CreateCronValidatorMock(false);
            var jsonValidator = WorkflowTestHelpers.CreateJsonValidatorMock(true);

            var sut = new WorkflowService(
                db,
                currentUserService.Object,
                cronValidator.Object,
                jsonValidator.Object);

            var request = new CreateWorkflowRequestDto
            {
                Name = "Bad Cron Workflow",
                IsEnabled = true,
                TriggerType = "Scheduled",
                CronExpression = "not-a-cron"
            };

            var result = await sut.CreateWorkflowAsync(request);

            result.Succeeded.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.Code == "workflow.cron_invalid");
            db.Workflow.Should().BeEmpty();
        }

        [Fact]
        public async Task CreateWorkflowAsync_ShouldTrimName_WhenSuccessful()
        {
            await using var db = WorkflowTestHelpers.CreateInMemoryDbContext();

            var currentUserService = WorkflowTestHelpers.CreateCurrentUserServiceMock(7);
            var cronValidator = WorkflowTestHelpers.CreateCronValidatorMock(true);
            var jsonValidator = WorkflowTestHelpers.CreateJsonValidatorMock(true);

            var sut = new WorkflowService(
                db,
                currentUserService.Object,
                cronValidator.Object,
                jsonValidator.Object);

            var request = new CreateWorkflowRequestDto
            {
                Name = "  Daily Sync  ",
                IsEnabled = true,
                TriggerType = "schedule",
                CronExpression = "0 0 * * *"
            };

            var result = await sut.CreateWorkflowAsync(request);

            result.Succeeded.Should().BeTrue();
            result.Data.Should().NotBeNull();
            result.Data!.Name.Should().Be("Daily Sync");

            var saved = await db.Workflow.SingleAsync();
            saved.Name.Should().Be("Daily Sync");
            saved.UserID.Should().Be(7);
        }

        [Fact]
        public async Task CreateWorkflowAsync_ShouldFail_WhenNameAlreadyExistsForSameUser()
        {
            await using var db = WorkflowTestHelpers.CreateInMemoryDbContext();

            db.Workflow.Add(new Workflow
            {
                UserID = 7,
                Name = "Duplicate Name",
                IsEnabled = true
            });

            await db.SaveChangesAsync();

            var currentUserService = WorkflowTestHelpers.CreateCurrentUserServiceMock(7);
            var cronValidator = WorkflowTestHelpers.CreateCronValidatorMock(true);
            var jsonValidator = WorkflowTestHelpers.CreateJsonValidatorMock(true);

            var sut = new WorkflowService(
                db,
                currentUserService.Object,
                cronValidator.Object,
                jsonValidator.Object);

            var request = new CreateWorkflowRequestDto
            {
                Name = "Duplicate Name",
                IsEnabled = false
            };

            var result = await sut.CreateWorkflowAsync(request);

            result.Succeeded.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.Code == "workflow.duplicate_name");
        }

        [Fact]
        public async Task CreateWorkflowAsync_ShouldAllowSameName_ForDifferentUser()
        {
            await using var db = WorkflowTestHelpers.CreateInMemoryDbContext();

            db.Workflow.Add(new Workflow
            {
                UserID = 99,
                Name = "Shared Name",
                IsEnabled = true
            });

            await db.SaveChangesAsync();

            var currentUserService = WorkflowTestHelpers.CreateCurrentUserServiceMock(7);
            var cronValidator = WorkflowTestHelpers.CreateCronValidatorMock(true);
            var jsonValidator = WorkflowTestHelpers.CreateJsonValidatorMock(true);

            var sut = new WorkflowService(
                db,
                currentUserService.Object,
                cronValidator.Object,
                jsonValidator.Object);

            var request = new CreateWorkflowRequestDto
            {
                Name = "Shared Name",
                IsEnabled = true
            };

            var result = await sut.CreateWorkflowAsync(request);

            result.Succeeded.Should().BeTrue();
            db.Workflow.Should().HaveCount(2);
        }

        [Fact]
        public async Task GetWorkflowsAsync_ShouldReturnOnlyCurrentUsersWorkflows_OrderedById()
        {
            await using var db = WorkflowTestHelpers.CreateInMemoryDbContext();

            db.Workflow.AddRange(
                new Workflow { UserID = 7, Name = "Second", IsEnabled = true },
                new Workflow { UserID = 99, Name = "Other User", IsEnabled = true },
                new Workflow { UserID = 7, Name = "Third", IsEnabled = true },
                new Workflow { UserID = 7, Name = "First", IsEnabled = true });

            await db.SaveChangesAsync();

            var expectedIds = await db.Workflow
                .Where(x => x.UserID == 7)
                .OrderBy(x => x.ID)
                .Select(x => x.ID)
                .ToListAsync();

            var currentUserService = WorkflowTestHelpers.CreateCurrentUserServiceMock(7);
            var cronValidator = WorkflowTestHelpers.CreateCronValidatorMock(true);
            var jsonValidator = WorkflowTestHelpers.CreateJsonValidatorMock(true);

            var sut = new WorkflowService(
                db,
                currentUserService.Object,
                cronValidator.Object,
                jsonValidator.Object);

            var result = await sut.GetWorkflowsAsync();

            result.Succeeded.Should().BeTrue();
            result.Data.Should().NotBeNull();
            result.Data!.Should().HaveCount(3);
            result.Data.Select(x => x.Id).Should().Equal(expectedIds);
        }

        [Fact]
        public async Task GetWorkflowByIdAsync_ShouldReturnWorkflowWithStepsOrderedByStepOrder()
        {
            await using var db = WorkflowTestHelpers.CreateInMemoryDbContext();

            var workflow = new Workflow
            {
                UserID = 7,
                Name = "Workflow A",
                IsEnabled = true
            };

            db.Workflow.Add(workflow);
            await db.SaveChangesAsync();

            db.WorkflowStep.AddRange(
                new WorkflowStep { WorkflowID = workflow.ID, StepType = "third", ConfigJson = "{\"x\":3}", StepOrder = 3 },
                new WorkflowStep { WorkflowID = workflow.ID, StepType = "first", ConfigJson = "{\"x\":1}", StepOrder = 1 },
                new WorkflowStep { WorkflowID = workflow.ID, StepType = "second", ConfigJson = "{\"x\":2}", StepOrder = 2 });
            await db.SaveChangesAsync();

            var currentUserService = WorkflowTestHelpers.CreateCurrentUserServiceMock(7);
            var cronValidator = WorkflowTestHelpers.CreateCronValidatorMock(true);
            var jsonValidator = WorkflowTestHelpers.CreateJsonValidatorMock(true);

            var sut = new WorkflowService(
                db,
                currentUserService.Object,
                cronValidator.Object,
                jsonValidator.Object);

            var result = await sut.GetWorkflowByIdAsync(workflow.ID);

            result.Succeeded.Should().BeTrue();
            result.Data.Should().NotBeNull();
            result.Data!.Steps.Select(x => x.StepType).Should().Equal("first", "second", "third");
            result.Data.Steps.Select(x => x.StepOrder).Should().Equal(1, 2, 3);
        }

        [Fact]
        public async Task GetWorkflowByIdAsync_ShouldReturnNotFound_WhenWorkflowIsOwnedByAnotherUser()
        {
            await using var db = WorkflowTestHelpers.CreateInMemoryDbContext();

            db.Workflow.Add(new Workflow
            {
                UserID = 99,
                Name = "Not Mine",
                IsEnabled = true
            });

            await db.SaveChangesAsync();

            var workflowId = await db.Workflow.Select(x => x.ID).SingleAsync();

            var currentUserService = WorkflowTestHelpers.CreateCurrentUserServiceMock(7);
            var cronValidator = WorkflowTestHelpers.CreateCronValidatorMock(true);
            var jsonValidator = WorkflowTestHelpers.CreateJsonValidatorMock(true);

            var sut = new WorkflowService(
                db,
                currentUserService.Object,
                cronValidator.Object,
                jsonValidator.Object);

            var result = await sut.GetWorkflowByIdAsync(workflowId);

            result.Succeeded.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.Code == "workflow.not_found");
        }

        [Fact]
        public async Task UpdateWorkflowAsync_ShouldFail_WhenWorkflowDoesNotExistForCurrentUser()
        {
            await using var db = WorkflowTestHelpers.CreateInMemoryDbContext();

            db.Workflow.Add(new Workflow
            {
                UserID = 99,
                Name = "Not Mine",
                IsEnabled = true
            });

            await db.SaveChangesAsync();

            var workflowId = await db.Workflow.Select(x => x.ID).SingleAsync();

            var currentUserService = WorkflowTestHelpers.CreateCurrentUserServiceMock(7);
            var cronValidator = WorkflowTestHelpers.CreateCronValidatorMock(true);
            var jsonValidator = WorkflowTestHelpers.CreateJsonValidatorMock(true);

            var sut = new WorkflowService(
                db,
                currentUserService.Object,
                cronValidator.Object,
                jsonValidator.Object);

            var request = new UpdateWorkflowRequestDto
            {
                Name = "Updated",
                IsEnabled = false,
                CronExpression = "0 0 * * *"
            };

            var result = await sut.UpdateWorkflowAsync(workflowId, request);

            result.Succeeded.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.Code == "workflow.not_found");
        }

        [Fact]
        public async Task UpdateWorkflowAsync_ShouldUpdateOwnedWorkflow()
        {
            await using var db = WorkflowTestHelpers.CreateInMemoryDbContext();

            db.Workflow.Add(new Workflow
            {
                UserID = 7,
                Name = "Original",
                IsEnabled = true,
                TriggerType = "schedule",
                CronExpression = "0 0 * * *"
            });

            await db.SaveChangesAsync();
            var workflowId = await db.Workflow.Select(x => x.ID).SingleAsync();

            var currentUserService = WorkflowTestHelpers.CreateCurrentUserServiceMock(7);
            var cronValidator = WorkflowTestHelpers.CreateCronValidatorMock(true);
            var jsonValidator = WorkflowTestHelpers.CreateJsonValidatorMock(true);

            var sut = new WorkflowService(
                db,
                currentUserService.Object,
                cronValidator.Object,
                jsonValidator.Object);

            var request = new UpdateWorkflowRequestDto
            {
                Name = " Updated Workflow ",
                IsEnabled = false,
                TriggerType = "manual",
                CronExpression = "*/5 * * * *"
            };

            var result = await sut.UpdateWorkflowAsync(workflowId, request);

            result.Succeeded.Should().BeTrue();
            result.Data.Should().NotBeNull();
            result.Data!.Name.Should().Be("Updated Workflow");
            result.Data.IsEnabled.Should().BeFalse();
            result.Data.TriggerType.Should().Be("manual");
            result.Data.CronExpression.Should().Be("*/5 * * * *");
        }

        [Fact]
        public async Task DeleteWorkflowAsync_ShouldReturnNotFound_WhenWorkflowDoesNotExistForCurrentUser()
        {
            await using var db = WorkflowTestHelpers.CreateInMemoryDbContext();

            db.Workflow.Add(new Workflow
            {
                UserID = 99,
                Name = "Other User Workflow",
                IsEnabled = true
            });

            await db.SaveChangesAsync();
            var workflowId = await db.Workflow.Select(x => x.ID).SingleAsync();

            var currentUserService = WorkflowTestHelpers.CreateCurrentUserServiceMock(7);
            var cronValidator = WorkflowTestHelpers.CreateCronValidatorMock(true);
            var jsonValidator = WorkflowTestHelpers.CreateJsonValidatorMock(true);

            var sut = new WorkflowService(
                db,
                currentUserService.Object,
                cronValidator.Object,
                jsonValidator.Object);

            var result = await sut.DeleteWorkflowAsync(workflowId);

            result.Succeeded.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.Code == "workflow.not_found");
        }

        [Fact]
        public async Task DeleteWorkflowAsync_ShouldDeleteOwnedWorkflow()
        {
            await using var db = WorkflowTestHelpers.CreateInMemoryDbContext();

            db.Workflow.Add(new Workflow
            {
                UserID = 7,
                Name = "Delete Me",
                IsEnabled = true
            });

            await db.SaveChangesAsync();
            var workflowId = await db.Workflow.Select(x => x.ID).SingleAsync();

            var currentUserService = WorkflowTestHelpers.CreateCurrentUserServiceMock(7);
            var cronValidator = WorkflowTestHelpers.CreateCronValidatorMock(true);
            var jsonValidator = WorkflowTestHelpers.CreateJsonValidatorMock(true);

            var sut = new WorkflowService(
                db,
                currentUserService.Object,
                cronValidator.Object,
                jsonValidator.Object);

            var result = await sut.DeleteWorkflowAsync(workflowId);

            result.Succeeded.Should().BeTrue();
            (await db.Workflow.AnyAsync(x => x.ID == workflowId)).Should().BeFalse();
        }

        [Fact]
        public async Task GetWorkflowStepsAsync_ShouldReturnOrderedSteps()
        {
            await using var db = WorkflowTestHelpers.CreateInMemoryDbContext();

            var workflow = new Workflow
            {
                UserID = 7,
                Name = "Steps Workflow",
                IsEnabled = true
            };

            db.Workflow.Add(workflow);
            await db.SaveChangesAsync();

            db.WorkflowStep.AddRange(
                new WorkflowStep { WorkflowID = workflow.ID, StepType = "third", ConfigJson = "{\"v\":3}", StepOrder = 3 },
                new WorkflowStep { WorkflowID = workflow.ID, StepType = "first", ConfigJson = "{\"v\":1}", StepOrder = 1 },
                new WorkflowStep { WorkflowID = workflow.ID, StepType = "second", ConfigJson = "{\"v\":2}", StepOrder = 2 });

            await db.SaveChangesAsync();

            var currentUserService = WorkflowTestHelpers.CreateCurrentUserServiceMock(7);
            var cronValidator = WorkflowTestHelpers.CreateCronValidatorMock(true);
            var jsonValidator = WorkflowTestHelpers.CreateJsonValidatorMock(true);

            var sut = new WorkflowService(
                db,
                currentUserService.Object,
                cronValidator.Object,
                jsonValidator.Object);

            var result = await sut.GetWorkflowStepsAsync(workflow.ID);

            result.Succeeded.Should().BeTrue();
            result.Data.Should().NotBeNull();
            result.Data!.WorkflowId.Should().Be(workflow.ID);
            result.Data.Steps.Select(x => x.StepType).Should().Equal("first", "second", "third");
            result.Data.Steps.Select(x => x.StepOrder).Should().Equal(1, 2, 3);
        }

        [Fact]
        public async Task CreateWorkflowStepsAsync_ShouldReturnNotFound_WhenWorkflowDoesNotExistForCurrentUser()
        {
            await using var db = WorkflowTestHelpers.CreateInMemoryDbContext();

            db.Workflow.Add(new Workflow
            {
                UserID = 99,
                Name = "Other User Workflow",
                IsEnabled = true
            });

            await db.SaveChangesAsync();
            var workflowId = await db.Workflow.Select(x => x.ID).SingleAsync();

            var currentUserService = WorkflowTestHelpers.CreateCurrentUserServiceMock(7);
            var cronValidator = WorkflowTestHelpers.CreateCronValidatorMock(true);
            var jsonValidator = WorkflowTestHelpers.CreateJsonValidatorMock(true);

            var sut = new WorkflowService(
                db,
                currentUserService.Object,
                cronValidator.Object,
                jsonValidator.Object);

            var request = new SaveWorkflowStepsRequestDto
            {
                Steps = []
            };

            var result = await sut.CreateWorkflowStepsAsync(workflowId, request);

            result.Succeeded.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.Code == "workflow.not_found");
        }

        [Fact]
        public async Task CreateWorkflowStepsAsync_ShouldAllowNullSteps()
        {
            await using var db = WorkflowTestHelpers.CreateInMemoryDbContext();

            var workflow = new Workflow
            {
                UserID = 7,
                Name = "Null Steps Workflow",
                IsEnabled = true
            };

            db.Workflow.Add(workflow);
            await db.SaveChangesAsync();

            var currentUserService = WorkflowTestHelpers.CreateCurrentUserServiceMock(7);
            var cronValidator = WorkflowTestHelpers.CreateCronValidatorMock(true);
            var jsonValidator = WorkflowTestHelpers.CreateJsonValidatorMock(true);

            var sut = new WorkflowService(
                db,
                currentUserService.Object,
                cronValidator.Object,
                jsonValidator.Object);

            var request = new SaveWorkflowStepsRequestDto
            {
                Steps = null!
            };

            var result = await sut.CreateWorkflowStepsAsync(workflow.ID, request);

            result.Succeeded.Should().BeTrue();
            result.Data.Should().NotBeNull();
            result.Data!.Steps.Should().BeEmpty();
            (await db.WorkflowStep.AnyAsync(x => x.WorkflowID == workflow.ID)).Should().BeFalse();
        }

        [Fact]
        public async Task CreateWorkflowStepsAsync_ShouldAllowEmptyList()
        {
            await using var db = WorkflowTestHelpers.CreateInMemoryDbContext();

            var workflow = new Workflow
            {
                UserID = 7,
                Name = "Empty Steps Workflow",
                IsEnabled = true
            };

            db.Workflow.Add(workflow);
            await db.SaveChangesAsync();

            var currentUserService = WorkflowTestHelpers.CreateCurrentUserServiceMock(7);
            var cronValidator = WorkflowTestHelpers.CreateCronValidatorMock(true);
            var jsonValidator = WorkflowTestHelpers.CreateJsonValidatorMock(true);

            var sut = new WorkflowService(
                db,
                currentUserService.Object,
                cronValidator.Object,
                jsonValidator.Object);

            var request = new SaveWorkflowStepsRequestDto
            {
                Steps = []
            };

            var result = await sut.CreateWorkflowStepsAsync(workflow.ID, request);

            result.Succeeded.Should().BeTrue();
            result.Data.Should().NotBeNull();
            result.Data!.Steps.Should().BeEmpty();
            (await db.WorkflowStep.AnyAsync(x => x.WorkflowID == workflow.ID)).Should().BeFalse();
        }

        [Fact]
        public async Task CreateWorkflowStepsAsync_ShouldCreateSteps_WithSequentialServerAssignedOrder()
        {
            await using var db = WorkflowTestHelpers.CreateInMemoryDbContext();

            var workflow = new Workflow
            {
                UserID = 7,
                Name = "Create Steps Workflow",
                IsEnabled = true
            };

            db.Workflow.Add(workflow);
            await db.SaveChangesAsync();

            var currentUserService = WorkflowTestHelpers.CreateCurrentUserServiceMock(7);
            var cronValidator = WorkflowTestHelpers.CreateCronValidatorMock(true);
            var jsonValidator = WorkflowTestHelpers.CreateJsonValidatorMock(true);

            var sut = new WorkflowService(
                db,
                currentUserService.Object,
                cronValidator.Object,
                jsonValidator.Object);

            var request = new SaveWorkflowStepsRequestDto
            {
                Steps = 
                [
                    new WorkflowStepItemDto { StepType = "http", ConfigJson = "{\"url\":\"https://example.com\"}" },
                    new WorkflowStepItemDto { StepType = "email", ConfigJson = "{\"to\":\"a@example.com\"}" },
                    new WorkflowStepItemDto { StepType = "delay", ConfigJson = "{\"seconds\":10}" }
                ]
            };

            var result = await sut.CreateWorkflowStepsAsync(workflow.ID, request);

            result.Succeeded.Should().BeTrue();
            result.Data.Should().NotBeNull();
            result.Data!.Steps.Select(x => x.StepOrder).Should().Equal(1, 2, 3);
            result.Data.Steps.Select(x => x.StepType).Should().Equal("http", "email", "delay");
        }

        [Fact]
        public async Task CreateWorkflowStepsAsync_ShouldTrimStepType()
        {
            await using var db = WorkflowTestHelpers.CreateInMemoryDbContext();

            var workflow = new Workflow
            {
                UserID = 7,
                Name = "Trim Step Workflow",
                IsEnabled = true
            };

            db.Workflow.Add(workflow);
            await db.SaveChangesAsync();

            var currentUserService = WorkflowTestHelpers.CreateCurrentUserServiceMock(7);
            var cronValidator = WorkflowTestHelpers.CreateCronValidatorMock(true);
            var jsonValidator = WorkflowTestHelpers.CreateJsonValidatorMock(true);

            var sut = new WorkflowService(
                db,
                currentUserService.Object,
                cronValidator.Object,
                jsonValidator.Object);

            var request = new SaveWorkflowStepsRequestDto
            {
                Steps =
                [
                    new WorkflowStepItemDto 
                    { 
                        StepType = "   http    ", 
                        ConfigJson = "{\"url\":\"https://example.com\"}" 
                    }
                ]
            };

            var result = await sut.CreateWorkflowStepsAsync(workflow.ID, request);

            result.Succeeded.Should().BeTrue();
            result.Data!.Steps.Single().StepType.Should().Be("http");
        }

        [Fact]
        public async Task CreateWorkflowStepsAsync_ShouldFail_WhenStepsAlreadyExist()
        {
            await using var db = WorkflowTestHelpers.CreateInMemoryDbContext();

            var workflow = new Workflow
            {
                UserID = 7,
                Name = "Existing Steps Workflow",
                IsEnabled = true
            };

            db.Workflow.Add(workflow);
            await db.SaveChangesAsync();

            db.WorkflowStep.Add(new WorkflowStep
            {
                WorkflowID = workflow.ID,
                StepType = "existing",
                ConfigJson = "{\"a\":1}",
                StepOrder = 1
            });

            await db.SaveChangesAsync();

            var currentUserService = WorkflowTestHelpers.CreateCurrentUserServiceMock(7);
            var cronValidator = WorkflowTestHelpers.CreateCronValidatorMock(true);
            var jsonValidator = WorkflowTestHelpers.CreateJsonValidatorMock(true);

            var sut = new WorkflowService(
                db,
                currentUserService.Object,
                cronValidator.Object,
                jsonValidator.Object);

            var request = new SaveWorkflowStepsRequestDto
            {
                Steps =
                [
                    new WorkflowStepItemDto
                    {
                        StepType = "new-step",
                        ConfigJson = "{\"b\":2}"
                    }
                ]
            };

            var result = await sut.CreateWorkflowStepsAsync(workflow.ID, request);

            result.Succeeded.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.Code == "workflow_steps.already_exist");
            db.WorkflowStep.Where(x => x.WorkflowID == workflow.ID).Should().HaveCount(1);
        }

        [Fact]
        public async Task CreateWorkflowStepsAsync_ShouldFail_WhenStepTypeIsBlank()
        {
            await using var db = WorkflowTestHelpers.CreateInMemoryDbContext();

            var workflow = new Workflow
            {
                UserID = 7,
                Name = "Invalid Step Type Workflow",
                IsEnabled = true
            };

            db.Workflow.Add(workflow);
            await db.SaveChangesAsync();

            var currentUserService = WorkflowTestHelpers.CreateCurrentUserServiceMock(7);
            var cronValidator = WorkflowTestHelpers.CreateCronValidatorMock(true);
            var jsonValidator = WorkflowTestHelpers.CreateJsonValidatorMock(true);

            var sut = new WorkflowService(
                db,
                currentUserService.Object,
                cronValidator.Object,
                jsonValidator.Object);

            var request = new SaveWorkflowStepsRequestDto
            {
                Steps =
                [
                    new WorkflowStepItemDto
                    {
                        StepType = "   ",
                        ConfigJson = "{\"a\":1}"
                    }
                ]
            };

            var result = await sut.CreateWorkflowStepsAsync(workflow.ID, request);

            result.Succeeded.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.Code == "workflow_step.type_required");
            db.WorkflowStep.Should().BeEmpty();
        }

        [Fact]
        public async Task CreateWorkflowStepsAsync_ShouldFail_WhenConfigJsonIsBlank()
        {
            await using var db = WorkflowTestHelpers.CreateInMemoryDbContext();

            var workflow = new Workflow
            {
                UserID = 7,
                Name = "Invalid Config Workflow",
                IsEnabled = true
            };

            db.Workflow.Add(workflow);
            await db.SaveChangesAsync();

            var currentUserService = WorkflowTestHelpers.CreateCurrentUserServiceMock(7);
            var cronValidator = WorkflowTestHelpers.CreateCronValidatorMock(true);
            var jsonValidator = WorkflowTestHelpers.CreateJsonValidatorMock(true);

            var sut = new WorkflowService(
                db,
                currentUserService.Object,
                cronValidator.Object,
                jsonValidator.Object);

            var request = new SaveWorkflowStepsRequestDto
            {
                Steps =
                [
                    new WorkflowStepItemDto
                    {
                        StepType = "http",
                        ConfigJson = "   "
                    }
                ]
            };

            var result = await sut.CreateWorkflowStepsAsync(workflow.ID, request);

            result.Succeeded.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.Code == "workflow_step.config_required");
            db.WorkflowStep.Should().BeEmpty();
        }

        [Fact]
        public async Task CreateWorkflowStepsAsync_ShouldFail_WhenConfigJsonIsInvalid()
        {
            await using var db = WorkflowTestHelpers.CreateInMemoryDbContext();

            var workflow = new Workflow
            {
                UserID = 7,
                Name = "Invalid Json Workflow",
                IsEnabled = true
            };

            db.Workflow.Add(workflow);
            await db.SaveChangesAsync();

            var currentUserService = WorkflowTestHelpers.CreateCurrentUserServiceMock(7);
            var cronValidator = WorkflowTestHelpers.CreateCronValidatorMock(true);
            var jsonValidator = WorkflowTestHelpers.CreateJsonValidatorMock(false);

            var sut = new WorkflowService(
                db,
                currentUserService.Object,
                cronValidator.Object,
                jsonValidator.Object);

            var request = new SaveWorkflowStepsRequestDto
            {
                Steps =
                [
                    new WorkflowStepItemDto
                    {
                        StepType = "http",
                        ConfigJson = "{invalid-json}"
                    }
                ]
            };

            var result = await sut.CreateWorkflowStepsAsync(workflow.ID, request);

            result.Succeeded.Should().BeFalse();
            result.Errors.Should().ContainSingle(e => e.Code == "workflow_step.config_required");
            db.WorkflowStep.Should().BeEmpty();
        }

        [Fact]
        public async Task ReplaceWorkflowStepsAsync_ShouldReplaceAllExistingSteps()
        {
            await using var db = WorkflowTestHelpers.CreateInMemoryDbContext();

            var workflow = new Workflow
            {
                UserID = 7,
                Name = "Replace Workflow",
                IsEnabled = true
            };

            db.Workflow.Add(workflow);
            await db.SaveChangesAsync();

            db.WorkflowStep.AddRange(
                new WorkflowStep { WorkflowID = workflow.ID, StepType = "old-1", ConfigJson = "{\"a\":1}", StepOrder = 1 },
                new WorkflowStep { WorkflowID = workflow.ID, StepType = "old-2", ConfigJson = "{\"b\":2}", StepOrder = 2 });

            await db.SaveChangesAsync();

            var currentUserService = WorkflowTestHelpers.CreateCurrentUserServiceMock(7);
            var cronValidator = WorkflowTestHelpers.CreateCronValidatorMock(true);
            var jsonValidator = WorkflowTestHelpers.CreateJsonValidatorMock(true);

            var sut = new WorkflowService(
                db,
                currentUserService.Object,
                cronValidator.Object,
                jsonValidator.Object);

            var request = new SaveWorkflowStepsRequestDto
            {
                Steps =
                [
                    new WorkflowStepItemDto { StepType = "new-1", ConfigJson = "{\"x\":1}" },
                    new WorkflowStepItemDto { StepType = "new-2", ConfigJson = "{\"y\":2}" },
                    new WorkflowStepItemDto { StepType = "new-3", ConfigJson = "{\"z\":3}" }
                ]
            };

            var result = await sut.ReplaceWorkflowStepsAsync(workflow.ID, request);

            result.Succeeded.Should().BeTrue();
            result.Data!.Steps.Select(x => x.StepType).Should().Equal("new-1", "new-2", "new-3");
            result.Data.Steps.Select(x => x.StepOrder).Should().Equal(1, 2, 3);

            var saved = await db.WorkflowStep
                .Where(x => x.WorkflowID == workflow.ID)
                .OrderBy(x => x.StepOrder)
                .ToListAsync();

            saved.Should().HaveCount(3);
            saved.Select(x => x.StepType).Should().Equal("new-1", "new-2", "new-3");
        }

        [Fact]
        public async Task ReplaceWorkflowStepsAsync_ShouldAllowNullSteps()
        {
            await using var db = WorkflowTestHelpers.CreateInMemoryDbContext();

            var workflow = new Workflow
            {
                UserID = 7,
                Name = "Replace Null Workflow",
                IsEnabled = true
            };

            db.Workflow.Add(workflow);
            await db.SaveChangesAsync();

            db.WorkflowStep.Add(new WorkflowStep
            {
                WorkflowID = workflow.ID,
                StepType = "existing",
                ConfigJson = "{\"a\":1}",
                StepOrder = 1
            });

            await db.SaveChangesAsync();

            var currentUserService = WorkflowTestHelpers.CreateCurrentUserServiceMock(7);
            var cronValidator = WorkflowTestHelpers.CreateCronValidatorMock(true);
            var jsonValidator = WorkflowTestHelpers.CreateJsonValidatorMock(true);

            var sut = new WorkflowService(
                db,
                currentUserService.Object,
                cronValidator.Object,
                jsonValidator.Object);

            var request = new SaveWorkflowStepsRequestDto
            {
                Steps = null!
            };

            var result = await sut.ReplaceWorkflowStepsAsync(workflow.ID, request);

            result.Succeeded.Should().BeTrue();
            result.Data!.Steps.Should().BeEmpty();
            (await db.WorkflowStep.AnyAsync(x => x.WorkflowID == workflow.ID)).Should().BeFalse();
        }

        [Fact]
        public async Task ReplaceWorkflowStepsAsync_ShouldAllowEmptyList()
        {
            await using var db = WorkflowTestHelpers.CreateInMemoryDbContext();

            var workflow = new Workflow
            {
                UserID = 7,
                Name = "Replace Empty Workflow",
                IsEnabled = true
            };

            db.Workflow.Add(workflow);
            await db.SaveChangesAsync();

            db.WorkflowStep.Add(new WorkflowStep
            {
                WorkflowID = workflow.ID,
                StepType = "existing",
                ConfigJson = "{\"a\":1}",
                StepOrder = 1
            });

            await db.SaveChangesAsync();

            var currentUserService = WorkflowTestHelpers.CreateCurrentUserServiceMock(7);
            var cronValidator = WorkflowTestHelpers.CreateCronValidatorMock(true);
            var jsonValidator = WorkflowTestHelpers.CreateJsonValidatorMock(true);

            var sut = new WorkflowService(
                db,
                currentUserService.Object,
                cronValidator.Object,
                jsonValidator.Object);

            var request = new SaveWorkflowStepsRequestDto
            {
                Steps = []
            };

            var result = await sut.ReplaceWorkflowStepsAsync(workflow.ID, request);

            result.Succeeded.Should().BeTrue();
            result.Data!.Steps.Should().BeEmpty();
            (await db.WorkflowStep.AnyAsync(x => x.WorkflowID == workflow.ID)).Should().BeFalse();
        }

        [Fact]
        public async Task DeleteWorkflowStepsAsync_ShouldReturnTrue_WhenNoStepsExist()
        {
            await using var db = WorkflowTestHelpers.CreateInMemoryDbContext();

            var workflow = new Workflow
            {
                UserID = 7,
                Name = "No Steps Workflow",
                IsEnabled = true
            };

            db.Workflow.Add(workflow);
            await db.SaveChangesAsync();

            var currentUserService = WorkflowTestHelpers.CreateCurrentUserServiceMock(7);
            var cronValidator = WorkflowTestHelpers.CreateCronValidatorMock(true);
            var jsonValidator = WorkflowTestHelpers.CreateJsonValidatorMock(true);

            var sut = new WorkflowService(
                db,
                currentUserService.Object,
                cronValidator.Object,
                jsonValidator.Object);

            var result = await sut.DeleteWorkflowStepsAsync(workflow.ID);

            result.Succeeded.Should().BeTrue();
            result.Data.Should().BeTrue();
        }

        [Fact]
        public async Task DeleteWorkflowStepsAsync_ShouldDeleteAllSteps_WhenTheyExist()
        {
            await using var db = WorkflowTestHelpers.CreateInMemoryDbContext();

            var workflow = new Workflow
            {
                UserID = 7,
                Name = "Delete Steps Workflow",
                IsEnabled = true
            };

            db.Workflow.Add(workflow);
            await db.SaveChangesAsync();

            db.WorkflowStep.AddRange(
                new WorkflowStep { WorkflowID = workflow.ID, StepType = "a", ConfigJson = "{\"a\":1}", StepOrder = 1 },
                new WorkflowStep { WorkflowID = workflow.ID, StepType = "b", ConfigJson = "{\"b\":2}", StepOrder = 2 });

            await db.SaveChangesAsync();

            var currentUserService = WorkflowTestHelpers.CreateCurrentUserServiceMock(7);
            var cronValidator = WorkflowTestHelpers.CreateCronValidatorMock(true);
            var jsonValidator = WorkflowTestHelpers.CreateJsonValidatorMock(true);

            var sut = new WorkflowService(
                db,
                currentUserService.Object,
                cronValidator.Object,
                jsonValidator.Object);

            var result = await sut.DeleteWorkflowStepsAsync(workflow.ID);

            result.Succeeded.Should().BeTrue();
            result.Data.Should().BeTrue();
            (await db.WorkflowStep.AnyAsync(x => x.WorkflowID == workflow.ID)).Should().BeFalse();
        }
    }
}
