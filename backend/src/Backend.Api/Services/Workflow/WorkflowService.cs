using Backend.Api.Data;
using Backend.Api.Models.Dtos.Workflow;
using Backend.Api.Models.Dtos.WorkflowStep;
using Backend.Api.Models.Entities;
using Backend.Api.Services.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace Backend.Api.Services.Workflow
{
    public sealed class WorkflowService : IWorkflowService
    {
        private static readonly ServiceError UnauthorizedError =
            new("auth.unauthorized", "The current user could not be determined.");

        private readonly AppDbContext _dbContext;
        private readonly ICurrentUserService _currentUserService;
        private readonly ICronExpressionValidator _cronExpressionValidator;
        private readonly IJsonValidationHelper _jsonValidationHelper;

        public WorkflowService(AppDbContext dbContext, ICurrentUserService currentUserService, ICronExpressionValidator cronExpressionValidator, IJsonValidationHelper jsonValidationHelper)
        {
            _dbContext = dbContext;
            _currentUserService = currentUserService;
            _cronExpressionValidator = cronExpressionValidator;
            _jsonValidationHelper = jsonValidationHelper;
        }

        public async Task<ServiceResult<WorkflowResponseDto>> CreateWorkflowAsync(CreateWorkflowRequestDto request, CancellationToken cancellationToken = default)
        {
            var userIdResult = TryGetCurrentUserId();
            if (!userIdResult.Succeeded)
            {
                return ServiceResult<WorkflowResponseDto>.Fail(userIdResult.Errors);
            }

            var userId = userIdResult.Data;
            var validationErrors = ValidateWorkflowRequest(request);

            if (validationErrors.Count > 0)
            {
                return ServiceResult<WorkflowResponseDto>.Fail(validationErrors);
            }

            var nameConflict = await _dbContext.Workflow
                .AnyAsync(
                    w => w.Name == request.Name && w.UserID == userId,
                    cancellationToken);

            if (nameConflict)
            {
                return ServiceResult<WorkflowResponseDto>.Fail(
                    new ServiceError("workflow.duplicate_name",
                    $"A workflow with name {request.Name.Trim()} already exists for this user."));
            }

            var workflow = new Models.Entities.Workflow
            {
                UserID = userId,
                Name = request.Name.Trim(),
                IsEnabled = request.IsEnabled,
                TriggerType = request.TriggerType,
                CronExpression = request.CronExpression                
            };

            _dbContext.Workflow.Add(workflow);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return ServiceResult<WorkflowResponseDto>.Ok(MapWorkflowResponse(workflow));
        }

        public async Task<ServiceResult<List<WorkflowResponseDto>>> GetWorkflowsAsync(CancellationToken cancellationToken = default)
        {
            var userIdResult = TryGetCurrentUserId();
            if (!userIdResult.Succeeded)
            {
                return ServiceResult<List<WorkflowResponseDto>>.Fail(userIdResult.Errors);
            }

            var userId = userIdResult.Data;

            var workflows = await _dbContext.Workflow
                .AsNoTracking()
                .Where(w => w.UserID == userId)
                .OrderBy(w => w.ID)
                .Select(w => new WorkflowResponseDto
                {
                    Id = w.ID,
                    Name = w.Name,
                    IsEnabled = w.IsEnabled,
                    TriggerType = w.TriggerType,
                    CronExpression = w.CronExpression,
                    CreatedAt = w.CreatedAt,
                    UpdatedAt = w.UpdatedAt
                })
                .ToListAsync(cancellationToken);

            return ServiceResult<List<WorkflowResponseDto>>.Ok(workflows);
        }

        public async Task<ServiceResult<WorkflowDetailResponseDto>> GetWorkflowByIdAsync(int workflowId, CancellationToken cancellationToken = default)
        {
            var userIdResult = TryGetCurrentUserId();
            if (!userIdResult.Succeeded)
            {
                return ServiceResult<WorkflowDetailResponseDto>.Fail(userIdResult.Errors);
            }

            var userId = userIdResult.Data;

            var workflow = await _dbContext.Workflow
                .AsNoTracking()
                .Include(w => w.WorkflowSteps)
                .FirstOrDefaultAsync(
                    w => w.ID == workflowId && w.UserID == userId,
                    cancellationToken);

            if (workflow == null)
            {
                return ServiceResult<WorkflowDetailResponseDto>.Fail(
                    new ServiceError("workflow.not_found", "Workflow was not found"));
            }

            var response = new WorkflowDetailResponseDto
            {
                Id = workflowId,
                Name = workflow.Name,
                IsEnabled = workflow.IsEnabled,
                TriggerType = workflow.TriggerType,
                CronExpression = workflow.CronExpression,
                CreatedAt = workflow.CreatedAt,
                UpdatedAt = workflow.UpdatedAt,
                Steps = workflow.WorkflowSteps
                    .OrderBy(s => s.StepOrder)
                    .Select(MapWorkflowStepResponse)
                    .ToList()
            };

            return ServiceResult<WorkflowDetailResponseDto>.Ok(response);
        }

        public async Task<ServiceResult<WorkflowResponseDto>> UpdateWorkflowAsync(int workflowId, UpdateWorkflowRequestDto request, CancellationToken cancellationToken = default)
        {
            var userIdResult = TryGetCurrentUserId();
            if (!userIdResult.Succeeded)
            {
                return ServiceResult<WorkflowResponseDto>.Fail(userIdResult.Errors);
            }

            var userId = userIdResult.Data;
            var validationErrors = ValidateWorkflowRequest(request);

            if (validationErrors.Count > 0)
            {
                return ServiceResult<WorkflowResponseDto>.Fail(validationErrors);
            }

            var workflow = await _dbContext.Workflow
                .FirstOrDefaultAsync(
                    w => w.ID == workflowId && w.UserID == userId,
                    cancellationToken);

            if (workflow == null)
            {
                return ServiceResult<WorkflowResponseDto>.Fail(
                    new ServiceError("workflow.not_found", "Workflow was not found."));
            }

            workflow.Name = request.Name.Trim();
            workflow.IsEnabled = request.IsEnabled;
            workflow.TriggerType = request.TriggerType;
            workflow.CronExpression = request.CronExpression;

            await _dbContext.SaveChangesAsync(cancellationToken);

            return ServiceResult<WorkflowResponseDto>.Ok(MapWorkflowResponse(workflow));
        }

        public async Task<ServiceResult<bool>> DeleteWorkflowAsync(int workflowId, CancellationToken cancellationToken = default)
        {
            var userIdResult = TryGetCurrentUserId();
            if (!userIdResult.Succeeded)
            {
                return ServiceResult<bool>.Fail(userIdResult.Errors);
            }

            var userId = userIdResult.Data;

            var workflow = await _dbContext.Workflow
                .FirstOrDefaultAsync(
                    w => w.ID == workflowId && w.UserID == userId,
                    cancellationToken);

            if (workflow == null)
            {
                return ServiceResult<bool>.Fail(
                    new ServiceError("workflow.not_found", "Workflow was not found"));
            }

            _dbContext.Workflow.Remove(workflow);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return ServiceResult<bool>.Ok(true);
        }

        public async Task<ServiceResult<WorkflowStepListResponseDto>> GetWorkflowStepsAsync(int workflowId, CancellationToken cancellationToken = default)
        {
            var userIdResult = TryGetCurrentUserId();
            if (!userIdResult.Succeeded)
            {
                return ServiceResult<WorkflowStepListResponseDto>.Fail(userIdResult.Errors);
            }

            var userId = userIdResult.Data;

            var workflow = await _dbContext.Workflow
                .FirstOrDefaultAsync(
                    w => w.ID == workflowId && w.UserID == userId,
                    cancellationToken);

            if (workflow == null)
            {
                return ServiceResult<WorkflowStepListResponseDto>.Fail(
                    new ServiceError("workflow.not_found", "Workflow was not found"));
            }

            var steps = await _dbContext.WorkflowStep
                .AsNoTracking()
                .Where(s => s.WorkflowID == workflowId)
                .OrderBy(s => s.StepOrder)
                .Select(s => new WorkflowStepResponseDto
                {
                    Id = s.ID,
                    WorkflowId = s.WorkflowID,
                    StepType = s.StepType,
                    ConfigJson = s.ConfigJson,
                    StepOrder = s.StepOrder,
                    CreatedAt = s.CreatedAt,
                    UpdatedAt = s.UpdatedAt
                })
                .ToListAsync(cancellationToken);

            return ServiceResult<WorkflowStepListResponseDto>.Ok(new WorkflowStepListResponseDto
            {
                WorkflowId = workflowId,
                Steps = steps
            });
        }

        public async Task<ServiceResult<WorkflowStepListResponseDto>> CreateWorkflowStepsAsync(int workflowId, SaveWorkflowStepsRequestDto request, CancellationToken cancellationToken = default)
        {
            var userIdResult = TryGetCurrentUserId();
            if (!userIdResult.Succeeded)
            {
                return ServiceResult<WorkflowStepListResponseDto>.Fail(userIdResult.Errors);
            }

            var userId = userIdResult.Data;

            var validationErrors = ValidateSaveWorkflowStepsRequest(request);
            if (validationErrors.Count() > 0)
            {
                return ServiceResult<WorkflowStepListResponseDto>.Fail(validationErrors);
            }

            var workflow = await _dbContext.Workflow
                .Include(w => w.WorkflowSteps)
                .FirstOrDefaultAsync(
                    w => w.ID == workflowId && w.UserID == userId,
                    cancellationToken);

            if (workflow == null)
            {
                return ServiceResult<WorkflowStepListResponseDto>.Fail(
                    new ServiceError("workflow.not_found", "Workflow was not found"));
            }

            if (workflow.WorkflowSteps.Any())
            {
                return ServiceResult<WorkflowStepListResponseDto>.Fail(
                    new ServiceError("workflow_steps.already_exist", "Steps already exist for this workflow. Use PUT to replace them."));
            }

            var entities = BuildStepEntities(workflowId, request);

            _dbContext.WorkflowStep.AddRange(entities);
            await _dbContext.SaveChangesAsync(cancellationToken);

            var response = entities
                .OrderBy(s => s.StepOrder)
                .Select(MapWorkflowStepResponse)
                .ToList();

            return ServiceResult<WorkflowStepListResponseDto>.Ok(new WorkflowStepListResponseDto
            {
                WorkflowId = workflowId,
                Steps = response
            });
        }

        public async Task<ServiceResult<WorkflowStepListResponseDto>> ReplaceWorkflowStepsAsync(int workflowId, SaveWorkflowStepsRequestDto request, CancellationToken cancellationToken = default)
        {
            var userIdResult = TryGetCurrentUserId();
            if (!userIdResult.Succeeded)
            {
                return ServiceResult<WorkflowStepListResponseDto>.Fail(userIdResult.Errors);
            }

            var userId = userIdResult.Data;

            var validationErrors = ValidateSaveWorkflowStepsRequest(request);
            if (validationErrors.Count() > 0)
            {
                return ServiceResult<WorkflowStepListResponseDto>.Fail(validationErrors);
            }

            var workflow = await _dbContext.Workflow
                .FirstOrDefaultAsync(
                    w => w.ID == workflowId && w.UserID == userId,
                    cancellationToken);

            if (workflow == null)
            {
                return ServiceResult<WorkflowStepListResponseDto>.Fail(
                    new ServiceError("workflow.not_found", "Workflow was not found"));
            }

            var existingSteps = await _dbContext.WorkflowStep
                .Where(s => s.WorkflowID == workflowId)
                .ToListAsync(cancellationToken);

            if (existingSteps.Count() > 0)
            {
                _dbContext.WorkflowStep.RemoveRange(existingSteps);
            }

            var newSteps = BuildStepEntities(workflowId, request);
            if (newSteps.Count() > 0)
            {
                _dbContext.WorkflowStep.AddRange(newSteps);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);

            var response = newSteps
                .OrderBy(s => s.StepOrder)
                .Select(MapWorkflowStepResponse)
                .ToList();

            return ServiceResult<WorkflowStepListResponseDto>.Ok(new WorkflowStepListResponseDto
            {
                WorkflowId = workflowId,
                Steps = response
            });
        }

        public async Task<ServiceResult<bool>> DeleteWorkflowStepsAsync(int workflowId, CancellationToken cancellationToken = default)
        {
            var userIdResult = TryGetCurrentUserId();
            if (!userIdResult.Succeeded)
            {
                return ServiceResult<bool>.Fail(userIdResult.Errors);
            }

            var userId = userIdResult.Data;

            var workflow = await _dbContext.Workflow
                .FirstOrDefaultAsync(
                    w => w.ID == workflowId && w.UserID == userId,
                    cancellationToken);

            if (workflow == null)
            {
                return ServiceResult<bool>.Fail(
                    new ServiceError("workflow.not_found", "Workflow was not found"));
            }

            var existingSteps = await _dbContext.WorkflowStep
                .Where(s => s.WorkflowID == workflowId)
                .ToListAsync(cancellationToken);

            if (existingSteps.Count() > 0)
            {
                _dbContext.WorkflowStep.RemoveRange(existingSteps);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            return ServiceResult<bool>.Ok(true);
        }

        private ServiceResult<int> TryGetCurrentUserId()
        {
            try
            {
                var userId = _currentUserService.GetUserId();
                return ServiceResult<int>.Ok(userId);
            }
            catch
            {
                return ServiceResult<int>.Fail(UnauthorizedError);
            }
        }

        private List<ServiceError> ValidateWorkflowRequest(CreateWorkflowRequestDto request)
        {
            var errors = new List<ServiceError>();

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                errors.Add(new ServiceError("workflow.name_required", "Workflow name is required."));
            }

            if(!string.IsNullOrWhiteSpace(request.CronExpression) && !_cronExpressionValidator.IsValid(request.CronExpression.Trim()))
            {
                errors.Add(new ServiceError("workflow.cron_invalid", "Cron expression is invalid."));
            }

            return errors;
        }

        private List<ServiceError> ValidateWorkflowRequest(UpdateWorkflowRequestDto request)
        {
            var errors = new List<ServiceError>();

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                errors.Add(new ServiceError("workflow.name_required", "Workflow name is required."));
            }

            if (!string.IsNullOrWhiteSpace(request.CronExpression) && !_cronExpressionValidator.IsValid(request.CronExpression.Trim()))
            {
                errors.Add(new ServiceError("workflow.cron_invalid", "Cron expression is invalid."));
            }

            return errors;
        }

        private List<ServiceError> ValidateSaveWorkflowStepsRequest(SaveWorkflowStepsRequestDto request)
        {
            var errors = new List<ServiceError>();

            var requestSteps = request.Steps ?? new List<WorkflowStepItemDto>();

            for (var i = 0; i < requestSteps.Count; i++)
            {
                var step = requestSteps[i];
                var stepNumber = i + 1;

                if (string.IsNullOrWhiteSpace(step.StepType))
                {
                    errors.Add(new ServiceError("workflow_step.type_required", $"Step {stepNumber}: type is required."));
                }

                if (string.IsNullOrWhiteSpace(step.ConfigJson) || !_jsonValidationHelper.IsValidJson(step.ConfigJson.Trim()))
                {
                    errors.Add(new ServiceError("workflow_step.config_required", $"Step {stepNumber}: config must be valid JSON."));
                }
            }
            
            return errors;
        }

        private static List<WorkflowStep> BuildStepEntities(int workflowId, SaveWorkflowStepsRequestDto request)
        {
            var steps = new List<WorkflowStep>();

            var requestSteps = request.Steps ?? new List<WorkflowStepItemDto>();

            for (var i = 0; i < requestSteps.Count; i++)
            {
                var step = requestSteps[i];

                steps.Add(new WorkflowStep
                {
                    WorkflowID = workflowId,
                    StepOrder = i + 1,
                    StepType = step.StepType.Trim(),
                    ConfigJson = step.ConfigJson
                });
            }

            return steps;
        }

        private static WorkflowResponseDto MapWorkflowResponse(Models.Entities.Workflow workflow)
        {
            return new WorkflowResponseDto
            {
                Id = workflow.ID,
                Name = workflow.Name,
                IsEnabled = workflow.IsEnabled,
                TriggerType = workflow.TriggerType,
                CronExpression = workflow.CronExpression,
                CreatedAt = workflow.CreatedAt,
                UpdatedAt = workflow.UpdatedAt
            };
        }

        private static WorkflowStepResponseDto MapWorkflowStepResponse(WorkflowStep step)
        {
            return new WorkflowStepResponseDto
            {
                Id = step.ID,
                WorkflowId = step.WorkflowID,
                StepType = step.StepType,
                ConfigJson = step.ConfigJson,
                StepOrder = step.StepOrder,
                CreatedAt = step.CreatedAt,
                UpdatedAt = step.UpdatedAt
            };
        }
    }
}
