using Backend.Api.Configuration;
using Backend.Api.Data;
using Backend.Api.Models.Entities;
using Backend.Api.Models.Validation;
using Backend.Api.WorkflowEngine.Abstractions;
using Backend.Api.WorkflowEngine.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Backend.Api.WorkflowEngine.Execution
{
    public sealed class WorkflowRunner(AppDbContext dbContext, IStepExecutorRegistry registry, IClock clock, IOptions<WorkflowExecutionOptions> options) : IWorkflowRunner
    {
        public async Task<WorkflowRunResult> RunAsync(WorkflowRunRequest request, CancellationToken cancellationToken)
        {
            var execution = request.ExecutionRecord;
            var executionContext = new WorkflowExecutionContext(
                new(
                    execution.ID,
                    execution.TriggerType,
                    execution.StartedAt ?? clock.UtcNow,
                    execution.ScheduledFor,
                    execution.EffectiveDate,
                    execution.Timezone
                ),
                request.WorkflowInputs
            );

            using var workflowTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(options.Value.WorkflowTimeoutSeconds));
            using var workflowLinked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, workflowTimeout.Token);

            execution.Status = ExecutionStatuses.Running;
            execution.StartedAt = clock.UtcNow;
            await dbContext.SaveChangesAsync(CancellationToken.None);

            try
            {
                foreach (var step in request.OrderedSteps.OrderBy(x => x.StepOrder))
                {
                    var result = await ExecuteStepAsync(request.Workflow, step, execution, executionContext, workflowLinked.Token);

                    if (!result.Succeeded)
                    {
                        execution.Status = ExecutionStatuses.Failed;
                        execution.ErrorCode = result.ErrorCode;
                        execution.ErrorMessage = Sanitize(result.ErrorMessage);
                        execution.CompletedAt = clock.UtcNow;
                        await dbContext.SaveChangesAsync(CancellationToken.None);

                        return new(execution.ID, execution.Status, execution.ErrorCode, execution.ErrorMessage);
                    }

                    executionContext.AddStepOutput(step.StepKey, result.Output ?? NormalizedStepOutput.Empty);
                }

                execution.Status = ExecutionStatuses.Succeeded;
                execution.CompletedAt = clock.UtcNow;
                await dbContext.SaveChangesAsync(CancellationToken.None);

                return new(execution.ID, execution.Status, null, null);
            }
            catch (OperationCanceledException)
            {
                var callerCancelled = cancellationToken.IsCancellationRequested;
                execution.Status = ExecutionStatuses.Cancelled;
                execution.ErrorCode = callerCancelled ? ExecutionErrorCodes.WorkflowCancelled : ExecutionErrorCodes.WorkflowTimeout;
                execution.ErrorMessage = callerCancelled ? "The workflow execution was cancelled." : "The workflow execution exceeded its timeout.";
                execution.CompletedAt = clock.UtcNow;

                await MarkRunningStepCancelledAsync(execution.ID, execution.ErrorCode, execution.ErrorMessage);

                await dbContext.SaveChangesAsync(CancellationToken.None);

                return new(execution.ID, execution.Status, execution.ErrorCode, execution.ErrorMessage);
            }
        }

        private async Task<StepExecutionResult> ExecuteStepAsync(Workflow workflow, WorkflowStep step, WorkflowExecution execution, WorkflowExecutionContext executionContext, CancellationToken cancellationToken)
        {
            var record = new StepExecution
            {
                ID = Guid.NewGuid(),
                WorkflowExecutionID = execution.ID,
                WorkflowStepID = step.ID,
                StepKey = step.StepKey,
                StepType = step.StepType,
                StepOrder = step.StepOrder,
                Status = ExecutionStatuses.Running,
                CreatedAt = clock.UtcNow,
                StartedAt = clock.UtcNow
            };

            dbContext.StepExecution.Add(record);
            await dbContext.SaveChangesAsync(CancellationToken.None);

            if (!registry.TryGet(step.StepType, out var executor))
            {
                return await FailStepAsync(record, ExecutionErrorCodes.UnknownStepType, "The workflow step type is not registered.");
            }

            using var stepTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(options.Value.StepTimeoutSeconds));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, stepTimeout.Token);

            try
            {
                var result = await executor.ExecuteAsync(new StepExecutionContext(workflow, step, executionContext), linked.Token);

                if (!result.Succeeded)
                {
                    return await FailStepAsync(record, result.ErrorCode ?? ExecutionErrorCodes.StepFailed, result.ErrorMessage ?? "The workflow step failed.");
                }

                var output = result.Output ?? NormalizedStepOutput.Empty;

                record.Status = ExecutionStatuses.Succeeded;
                record.OutputJson = output.Json;
                record.CompletedAt = clock.UtcNow;
                await dbContext.SaveChangesAsync(CancellationToken.None);

                return StepExecutionResult.Success(output);
            }
            catch (NormalizedOutputTooLargeException)
            {
                return await FailStepAsync(record, ExecutionErrorCodes.OutputTooLarge, "The workflow step output exceeded the allowed size.");
            }
            catch (OperationCanceledException) when (stepTimeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                return await FailStepAsync(record, ExecutionErrorCodes.StepTimeout, "The workflow step exceeded its timeout.");
            }
            catch (OperationCanceledException)
            {
                record.Status = ExecutionStatuses.Cancelled;
                record.ErrorCode = ExecutionErrorCodes.WorkflowCancelled;
                record.ErrorMessage = "The workflow step was cancelled.";
                record.CompletedAt = clock.UtcNow;
                await dbContext.SaveChangesAsync(CancellationToken.None);
                throw;
            }
            catch (Exception)
            {
                return await FailStepAsync(record, ExecutionErrorCodes.StepFailed, "The workflow step failed.");
            }
        }

        private async Task<StepExecutionResult> FailStepAsync(StepExecution record, string errorCode, string safeMessage)
        {
            record.Status = ExecutionStatuses.Failed;
            record.ErrorCode = SanitizeCode(errorCode);
            record.ErrorMessage = Sanitize(safeMessage);
            record.CompletedAt = clock.UtcNow;
            await dbContext.SaveChangesAsync(CancellationToken.None);

            return StepExecutionResult.Failure(record.ErrorCode, record.ErrorMessage);
        }

        private async Task MarkRunningStepCancelledAsync(Guid executionId, string code, string message)
        {
            var runningStep = await dbContext.StepExecution.FirstOrDefaultAsync(
                x => x.WorkflowExecutionID == executionId && x.Status == ExecutionStatuses.Running,
                CancellationToken.None
            );

            if (runningStep is null)
            {
                return;
            }

            runningStep.Status = ExecutionStatuses.Cancelled;
            runningStep.ErrorCode = SanitizeCode(code);
            runningStep.ErrorMessage = Sanitize(message);
            runningStep.CompletedAt = clock.UtcNow;
        }

        private static string SanitizeCode(string? value)
        {
            var safe = string.IsNullOrWhiteSpace(value) ? ExecutionErrorCodes.StepFailed : value.Trim();
            return safe.Length <= WorkflowLimits.ExecutionErrorCodeMaxLength ? safe : safe[..WorkflowLimits.ExecutionErrorCodeMaxLength];
        }

        private static string Sanitize(string? value)
        {
            var safe = string.IsNullOrWhiteSpace(value) ? "The workflow execution failed." : value.Trim();
            return safe.Length <= WorkflowLimits.ExecutionErrorMessageMaxLength ? safe : safe[..WorkflowLimits.ExecutionErrorMessageMaxLength];
        }
    }
}
