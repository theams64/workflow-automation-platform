using Backend.Api.Data;
using Backend.Api.Models.Dtos.Workflow;
using Backend.Api.Models.Dtos.WorkflowStep;
using Backend.Api.Models.Entities;
using Backend.Api.Models.Validation;
using Backend.Api.Services.Common;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Backend.Api.Services.Workflow
{
    public sealed class WorkflowService : IWorkflowService
    {
        private const string WorkflowNameConstraint = "IX_workflow_user_id_name";

        private const string WorkflowStepOrderConstraint = "IX_workflow_step_workflow_id_step_order";

        private static readonly HashSet<string> AllowedTriggerTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "manual",
            "schedule"
        };

        private static readonly ServiceError UnauthorizedError = new("auth.unauthorized", "The current user could not be determined.");

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

            var validationErrors = ValidateWorkflowRequest(request);

            if (validationErrors.Count > 0)
            {
                return ServiceResult<WorkflowResponseDto>.Fail(validationErrors);
            }

            var userId = userIdResult.Data;
            var normalizedName = request.Name.Trim();

            var nameConflict = await _dbContext.Workflow
                .AsNoTracking()
                .AnyAsync(workflow => workflow.UserID == userId && workflow.Name == normalizedName, cancellationToken);

            if (nameConflict)
            {
                return DuplicateWorkflowNameFailure();
            }

            var workflow = new Models.Entities.Workflow
            {
                UserID = userId,
                Name = normalizedName,
                IsEnabled = request.IsEnabled!.Value,
                TriggerType = NormalizeTriggerType(request.TriggerType),
                CronExpression = NormalizeOptionalValue(request.CronExpression)
            };

            _dbContext.Workflow.Add(workflow);

            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception, WorkflowNameConstraint))
            {
                return DuplicateWorkflowNameFailure();
            }

            return ServiceResult<WorkflowResponseDto>.Ok(MapWorkflowResponse(workflow));
        }

        public async Task<ServiceResult<WorkflowListResponseDto>> GetWorkflowsAsync(WorkflowListRequestDto request, CancellationToken cancellationToken = default)
        {
            var userIdResult = TryGetCurrentUserId();

            if (!userIdResult.Succeeded)
            {
                return ServiceResult<WorkflowListResponseDto>.Fail(userIdResult.Errors);
            }

            var validationErrors = ValidatePaginationRequest(request);

            if (validationErrors.Count > 0)
            {
                return ServiceResult<WorkflowListResponseDto>.Fail(validationErrors);
            }

            var userId = userIdResult.Data;

            var ownedWorkflows = _dbContext.Workflow
                .AsNoTracking()
                .Where(workflow => workflow.UserID == userId);

            var totalCount = await ownedWorkflows.CountAsync(cancellationToken);

            var skip = checked((request.Page - 1) * request.PageSize);

            var workflows = await ownedWorkflows
                .OrderBy(workflow => workflow.ID)
                .Skip(skip)
                .Take(request.PageSize)
                .Select(workflow => new WorkflowResponseDto
                {
                    Id = workflow.ID,
                    Name = workflow.Name,
                    IsEnabled = workflow.IsEnabled,
                    TriggerType = workflow.TriggerType,
                    CronExpression = workflow.CronExpression,
                    CreatedAt = workflow.CreatedAt,
                    UpdatedAt = workflow.UpdatedAt
                })
                .ToListAsync(cancellationToken);

            return ServiceResult<WorkflowListResponseDto>.Ok(new WorkflowListResponseDto
            {
                Items = workflows,
                Page = request.Page,
                PageSize = request.PageSize,
                TotalCount = totalCount
            });
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
                .Where(candidate => candidate.ID == workflowId && candidate.UserID == userId)
                .Select(candidate => new WorkflowDetailResponseDto
                {
                    Id = candidate.ID,
                    Name = candidate.Name,
                    IsEnabled = candidate.IsEnabled,
                    TriggerType = candidate.TriggerType,
                    CronExpression = candidate.CronExpression,
                    CreatedAt = candidate.CreatedAt,
                    UpdatedAt = candidate.UpdatedAt
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (workflow is null)
            {
                return WorkflowNotFoundFailure<WorkflowDetailResponseDto>();
            }

            workflow.Steps = await _dbContext.WorkflowStep
                .AsNoTracking()
                .Where(step => step.WorkflowID == workflowId)
                .OrderBy(step => step.StepOrder)
                .Take(WorkflowLimits.MaxStepsPerWorkflow)
                .Select(step => new WorkflowStepResponseDto 
                {
                    Id = step.ID,
                    WorkflowId = step.WorkflowID,
                    StepType = step.StepType,
                    ConfigJson = step.ConfigJson,
                    StepOrder = step.StepOrder,
                    CreatedAt = step.CreatedAt,
                    UpdatedAt = step.UpdatedAt
                })
                .ToListAsync(cancellationToken);

            return ServiceResult<WorkflowDetailResponseDto>.Ok(workflow);
        }

        public async Task<ServiceResult<WorkflowResponseDto>> UpdateWorkflowAsync(int workflowId, UpdateWorkflowRequestDto request, CancellationToken cancellationToken = default)
        {
            var userIdResult = TryGetCurrentUserId();

            if (!userIdResult.Succeeded)
            {
                return ServiceResult<WorkflowResponseDto>.Fail(userIdResult.Errors);
            }

            var validationErrors = ValidateWorkflowRequest(request);

            if (validationErrors.Count > 0)
            {
                return ServiceResult<WorkflowResponseDto>.Fail(validationErrors);
            }

            var userId = userIdResult.Data;
            var normalizedName = request.Name.Trim();

            var workflow = await _dbContext.Workflow
                .FirstOrDefaultAsync(candidate => candidate.ID == workflowId && candidate.UserID == userId, cancellationToken);

            if (workflow is null)
            {
                return WorkflowNotFoundFailure<WorkflowResponseDto>();
            }

            var nameConflict = await _dbContext.Workflow
                .AsNoTracking()
                .AnyAsync(candidate => candidate.UserID == userId && candidate.ID != workflowId && candidate.Name == normalizedName, cancellationToken);

            if (nameConflict)
            {
                return DuplicateWorkflowNameFailure();
            }

            workflow.Name = normalizedName;
            workflow.IsEnabled = request.IsEnabled!.Value;
            workflow.TriggerType = NormalizeTriggerType(request.TriggerType);
            workflow.CronExpression = NormalizeOptionalValue(request.CronExpression);

            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception, WorkflowNameConstraint))
            {
                return DuplicateWorkflowNameFailure();
            }

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
                .FirstOrDefaultAsync(candidate => candidate.ID == workflowId && candidate.UserID == userId,cancellationToken);

            if (workflow is null)
            {
                return WorkflowNotFoundFailure<bool>();
            }

            _dbContext.Workflow.Remove(workflow);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return ServiceResult<bool>.Ok(true);
        }

        public async Task<ServiceResult<WorkflowStepListResponseDto>> GetWorkflowStepsAsync(int workflowId, CancellationToken cancellationToken = default)
        {
            var ownershipResult = await VerifyWorkflowOwnershipAsync(workflowId, cancellationToken);

            if (!ownershipResult.Succeeded)
            {
                return ServiceResult<WorkflowStepListResponseDto>.Fail(ownershipResult.Errors);
            }

            var steps = await _dbContext.WorkflowStep
                .AsNoTracking()
                .Where(step => step.WorkflowID == workflowId)
                .OrderBy(step => step.StepOrder)
                .Take(WorkflowLimits.MaxStepsPerWorkflow)
                .Select(step => new WorkflowStepResponseDto
                {
                    Id = step.ID,
                    WorkflowId = step.WorkflowID,
                    StepType = step.StepType,
                    ConfigJson = step.ConfigJson,
                    StepOrder = step.StepOrder,
                    CreatedAt = step.CreatedAt,
                    UpdatedAt = step.UpdatedAt
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
            var validationErrors = ValidateSaveWorkflowStepsRequest(request, allowEmptyCollection: false);

            if (validationErrors.Count > 0)
            {
                return ServiceResult<WorkflowStepListResponseDto>.Fail(validationErrors);
            }

            var ownershipResult = await VerifyWorkflowOwnershipAsync(workflowId, cancellationToken);

            if (!ownershipResult.Succeeded)
            {
                return ServiceResult<WorkflowStepListResponseDto>.Fail(ownershipResult.Errors);
            }

            var stepsAlreadyExist = await _dbContext.WorkflowStep
                .AsNoTracking()
                .AnyAsync(step => step.WorkflowID == workflowId, cancellationToken);

            if (stepsAlreadyExist)
            {
                return StepsAlreadyExistFailure();
            }

            var entities = BuildStepEntities(workflowId, request.Steps);
            _dbContext.WorkflowStep.AddRange(entities);

            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception, WorkflowStepOrderConstraint))
            {
                return StepsAlreadyExistFailure();
            }

            return ServiceResult<WorkflowStepListResponseDto>.Ok(BuildStepListResponse(workflowId, entities));
        }

        public async Task<ServiceResult<WorkflowStepListResponseDto>> ReplaceWorkflowStepsAsync(int workflowId, SaveWorkflowStepsRequestDto request, CancellationToken cancellationToken = default)
        {
            var validationErrors = ValidateSaveWorkflowStepsRequest(request, allowEmptyCollection: true);

            if (validationErrors.Count > 0)
            {
                return ServiceResult<WorkflowStepListResponseDto>.Fail(validationErrors);
            }

            var ownershipResult = await VerifyWorkflowOwnershipAsync(workflowId, cancellationToken);

            if (!ownershipResult.Succeeded)
            {
                return ServiceResult<WorkflowStepListResponseDto>.Fail(ownershipResult.Errors);
            }

            var replacementSteps = BuildStepEntities(workflowId, request.Steps);

            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            try
            {
                await _dbContext.WorkflowStep
                    .Where(step => step.WorkflowID == workflowId)
                    .ExecuteDeleteAsync(cancellationToken);

                if (replacementSteps.Count > 0)
                {
                    _dbContext.WorkflowStep.AddRange(replacementSteps);

                    await _dbContext.SaveChangesAsync(cancellationToken);
                }

                await transaction.CommitAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception, WorkflowStepOrderConstraint))
            {
                await transaction.RollbackAsync(cancellationToken);

                return ServiceResult<WorkflowStepListResponseDto>.Fail(new ServiceError("workflow_steps.conflict", "The workflow steps changed concurrently. Reload and try again."));
            }

            return ServiceResult<WorkflowStepListResponseDto>.Ok(BuildStepListResponse(workflowId, replacementSteps));
        }

        public async Task<ServiceResult<bool>> DeleteWorkflowStepsAsync(int workflowId, CancellationToken cancellationToken = default)
        {
            var ownershipResult = await VerifyWorkflowOwnershipAsync(workflowId, cancellationToken);

            if (!ownershipResult.Succeeded)
            {
                return ServiceResult<bool>.Fail(ownershipResult.Errors);
            }

            await _dbContext.WorkflowStep
                .Where(step => step.WorkflowID == workflowId)
                .ExecuteDeleteAsync(cancellationToken);

            return ServiceResult<bool>.Ok(true);
        }

        private async Task<ServiceResult<bool>> VerifyWorkflowOwnershipAsync(int workflowId, CancellationToken cancellationToken)
        {
            var userIdResult = TryGetCurrentUserId();

            if (!userIdResult.Succeeded)
            {
                return ServiceResult<bool>.Fail(userIdResult.Errors);
            }

            var exists = await _dbContext.Workflow
                .AsNoTracking()
                .AnyAsync(workflow => workflow.ID == workflowId && workflow.UserID == userIdResult.Data, cancellationToken);

            if (!exists)
            {
                return WorkflowNotFoundFailure<bool>();
            }

            return ServiceResult<bool>.Ok(true);
        }

        private ServiceResult<int> TryGetCurrentUserId()
        {
            try
            {
                return ServiceResult<int>.Ok(_currentUserService.GetUserId());
            }
            catch (UnauthorizedAccessException)
            {
                return ServiceResult<int>.Fail(UnauthorizedError);
            }
        }

        private List<ServiceError> ValidateWorkflowRequest(CreateWorkflowRequestDto request)
        {
            return ValidateWorkflowValues(request.Name, request.IsEnabled, request.TriggerType, request.CronExpression);
        }

        private List<ServiceError> ValidateWorkflowRequest(UpdateWorkflowRequestDto request)
        {
            return ValidateWorkflowValues(request.Name, request.IsEnabled, request.TriggerType, request.CronExpression);
        }

        private List<ServiceError> ValidateWorkflowValues(string? name, bool? isEnabled, string? triggerType, string? cronExpression)
        {
            var errors = new List<ServiceError>();

            if (string.IsNullOrWhiteSpace(name))
            {
                errors.Add(new ServiceError("workflow.name_required", "Workflow name is required."));
            }
            else if (name.Trim().Length > WorkflowLimits.NameMaxLength)
            {
                errors.Add(new ServiceError("workflow.name_too_long", $"Workflow name cannot exceed {WorkflowLimits.NameMaxLength} characters."));
            }

            if (isEnabled is null)
            {
                errors.Add(new ServiceError("workflow.is_enabled_required", "IsEnabled is required."));
            }

            var normalizedTriggerType = NormalizeOptionalValue(triggerType);

            if (normalizedTriggerType is not null)
            {
                if (normalizedTriggerType.Length > WorkflowLimits.TriggerTypeMaxLength)
                {
                    errors.Add(new ServiceError("workflow.trigger_type_too_long", $"Trigger type cannot exceed {WorkflowLimits.TriggerTypeMaxLength} characters."));
                }
                else if (!AllowedTriggerTypes.Contains(normalizedTriggerType))
                {
                    errors.Add(new ServiceError("workflow.trigger_type_invalid", "Trigger type must be either 'manual' or 'schedule'."));
                }
            }

            var normalizedCron = NormalizeOptionalValue(cronExpression);

            if (normalizedCron is not null)
            {
                if (normalizedCron.Length > WorkflowLimits.CronExpressionMaxLength)
                {
                    errors.Add(new ServiceError("workflow.cron_too_long", $"Cron expression cannot exceed {WorkflowLimits.CronExpressionMaxLength} characters."));
                }
                else if (!_cronExpressionValidator.IsValid(normalizedCron))
                {
                    errors.Add(new ServiceError("workflow.cron_invalid", "Cron expression is invalid."));
                }
            }

            return errors;
        }

        private static List<ServiceError> ValidatePaginationRequest(WorkflowListRequestDto request)
        {
            var errors = new List<ServiceError>();

            if (request.Page < 1)
            {
                errors.Add(new ServiceError("pagination.page_invalid", "Page must be at least 1."));
            }

            if (request.PageSize < 1 || request.PageSize > WorkflowLimits.MaxPageSize)
            {
                errors.Add(new ServiceError("pagination.page_size_invalid", $"Page size must be between 1 and {WorkflowLimits.MaxPageSize}."));
            }

            return errors;
        }

        private List<ServiceError> ValidateSaveWorkflowStepsRequest(SaveWorkflowStepsRequestDto request, bool allowEmptyCollection)
        {
            var errors = new List<ServiceError>();

            if (request.Steps is null)
            {
                errors.Add(new ServiceError("workflow_steps.collection_required", "The steps collection is required."));

                return errors;
            }

            if (!allowEmptyCollection && request.Steps.Count == 0)
            {
                errors.Add(new ServiceError("workflow_steps.collection_empty", "At least one workflow step is required."));
            }

            if (request.Steps.Count > WorkflowLimits.MaxStepsPerWorkflow)
            {
                errors.Add(new ServiceError("workflow_steps.too_many", $"A workflow cannot contain more than {WorkflowLimits.MaxStepsPerWorkflow} steps."));

                return errors;
            }

            for (var i = 0; i < request.Steps.Count; i++)
            {
                var step = request.Steps[i];
                var stepNumber = i + 1;

                if (string.IsNullOrWhiteSpace(step.StepType))
                {
                    errors.Add(new ServiceError("workflow_step.type_required", $"Step {stepNumber}: type is required."));
                }
                else if (step.StepType.Trim().Length > WorkflowLimits.StepTypeMaxLength)
                {
                    errors.Add(new ServiceError("workflow_step.type_too_long", $"Step {stepNumber}: type cannot exceed {WorkflowLimits.StepTypeMaxLength} characters."));
                }

                if (string.IsNullOrWhiteSpace(step.ConfigJson))
                {
                    errors.Add(new ServiceError("workflow_step.config_required", $"Step {stepNumber}: configuration is required."));

                    continue;
                }

                if (step.ConfigJson.Length > WorkflowLimits.ConfigJsonMaxLength)
                {
                    errors.Add(new ServiceError("workflow_step.config_too_large", $"Step {stepNumber}: configuration cannot exceed {WorkflowLimits.ConfigJsonMaxLength} characters."));

                    continue;
                }

                if (!_jsonValidationHelper.IsValidJson(step.ConfigJson.Trim()))
                {
                    errors.Add(new ServiceError("workflow_step.config_invalid", $"Step {stepNumber}: configuration must be valid JSON."));
                }
            }

            return errors;
        }

        private static List<WorkflowStep> BuildStepEntities(int workflowId, IReadOnlyList<WorkflowStepItemDto> requestSteps)
        {
            var steps = new List<WorkflowStep>(requestSteps.Count);

            for (var i = 0; i < requestSteps.Count; i++)
            {
                var requestStep = requestSteps[i];

                steps.Add(new WorkflowStep
                {
                    WorkflowID = workflowId,
                    StepOrder = i + 1,
                    StepType = requestStep.StepType.Trim(),
                    ConfigJson = requestStep.ConfigJson.Trim()
                });
            }

            return steps;
        }

        private static WorkflowStepListResponseDto BuildStepListResponse(int workflowId, IEnumerable<WorkflowStep> steps)
        {
            return new WorkflowStepListResponseDto
            {
                WorkflowId = workflowId,
                Steps = steps
                    .OrderBy(step => step.StepOrder)
                    .Select(MapWorkflowStepResponse)
                    .ToList()
            };
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

        private static string? NormalizeOptionalValue(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static string? NormalizeTriggerType(string? triggerType)
        {
            return NormalizeOptionalValue(triggerType)?.ToLowerInvariant();
        }

        private static bool IsUniqueConstraintViolation(DbUpdateException exception, string expectedConstraint)
        {
            return exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgresException 
                && string.Equals(postgresException.ConstraintName, expectedConstraint, StringComparison.OrdinalIgnoreCase);
        }

        private static ServiceResult<WorkflowResponseDto> DuplicateWorkflowNameFailure()
        {
            return ServiceResult<WorkflowResponseDto>.Fail(new ServiceError("workflow.duplicate_name", "A workflow with this name already exists for the current user."));
        }

        private static ServiceResult<WorkflowStepListResponseDto> StepsAlreadyExistFailure()
        {
            return ServiceResult<WorkflowStepListResponseDto>.Fail(new ServiceError("workflow_steps.already_exist", "Steps already exist for this workflow. Use PUT to replace them."));
        }

        private static ServiceResult<T> WorkflowNotFoundFailure<T>()
        {
            return ServiceResult<T>.Fail(new ServiceError("workflow.not_found", "Workflow was not found."));
        }
    }
}
