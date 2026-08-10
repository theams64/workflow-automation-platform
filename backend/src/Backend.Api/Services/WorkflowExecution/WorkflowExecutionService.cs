using Backend.Api.Data;
using Backend.Api.Models.Validation;
using Backend.Api.Services.Common;
using Backend.Api.WorkflowEngine.Abstractions;
using Backend.Api.WorkflowEngine.Execution;
using Backend.Api.WorkflowEngine.Time;
using Backend.Api.WorkflowEngine.Validation;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;

namespace Backend.Api.Services.WorkflowExecution
{
    public sealed class WorkflowExecutionService(AppDbContext dbContext, ICurrentUserService currentUserService, IWorkflowValidationService validationService, IWorkflowRunner runner, IExecutionDateResolver dateResolver, IClock clock) : IWorkflowExecutionService
    {
        public async Task<ServiceResult<WorkflowRunResult>> ExecuteAsync(int workflowId, string triggerType, JsonElement workflowInputs, DateOnly? explicitDate = null, DateTimeOffset? scheduledFor = null, CancellationToken cancellationToken = default)
        {
            int userId;
            try
            {
                userId = currentUserService.GetUserId();
            }
            catch (UnauthorizedAccessException)
            {
                return ServiceResult<WorkflowRunResult>.Fail(new ServiceError("auth.unauthorized", "The current user could not be determined."));
            }

            var normalizedTrigger = triggerType?.Trim().ToLowerInvariant();
            if (normalizedTrigger is null || !WorkflowTriggerTypes.IsSupported(normalizedTrigger))
            {
                return ServiceResult<WorkflowRunResult>.Fail(new ServiceError("workflow_execution.trigger_invalid", "The execution trigger type is invalid."));
            }

            var inputJson = JsonSerializer.Serialize(workflowInputs);
            if (Encoding.UTF8.GetByteCount(inputJson) > WorkflowLimits.RuntimeInputMaxBytes)
            {
                return ServiceResult<WorkflowRunResult>.Fail(new ServiceError("workflow_execution.input_too_large", "The workflow input exceeded the allowed size."));
            }

            var workflow = await dbContext.Workflow
                .AsNoTracking()
                .Include(x => x.WorkflowSteps)
                .FirstOrDefaultAsync(x => x.ID == workflowId && x.UserID == userId, cancellationToken);

            if (workflow is null)
            {
                return ServiceResult<WorkflowRunResult>.Fail(new ServiceError("workflow.not_found", "Workflow was not found."));
            }

            var orderedSteps = workflow.WorkflowSteps
                .OrderBy(x => x.StepOrder)
                .ToList();

            var validation = validationService.Validate(orderedSteps, WorkflowValidationMode.Executable);

            if (!validation.Succeeded)
            {
                return ServiceResult<WorkflowRunResult>.Fail(validation.Errors);
            }

            var effectiveDate = dateResolver.Resolve(new(workflow.Timezone, explicitDate, scheduledFor));

            var execution = new Models.Entities.WorkflowExecution
            {
                ID = Guid.NewGuid(),
                WorkflowID = workflow.ID,
                InitiatingUserID = userId,
                TriggerType = normalizedTrigger,
                Status = ExecutionStatuses.Pending,
                CreatedAt = clock.UtcNow,
                ScheduledFor = scheduledFor,
                EffectiveDate = effectiveDate,
                Timezone = workflow.Timezone,
                InputJson = inputJson
            };

            dbContext.WorkflowExecution.Add(execution);
            await dbContext.SaveChangesAsync(cancellationToken);

            var runResult = await runner.RunAsync(new WorkflowRunRequest(workflow, orderedSteps, execution, workflowInputs), cancellationToken);

            return ServiceResult<WorkflowRunResult>.Ok(runResult);
        }
    }
}
