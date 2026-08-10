using Backend.Api.Services.Common;
using Backend.Api.WorkflowEngine.Abstractions;
using Backend.Api.WorkflowEngine.Execution;
using Backend.Api.WorkflowEngine.Validation;

namespace Backend.Api.Tests.Common
{
    public sealed class FakeStepExecutor : IWorkflowStepExecutor
    {
        private readonly Func<StepExecutionContext, CancellationToken, Task<StepExecutionResult>> _execute;
        private readonly Func<string, WorkflowValidationContext, IReadOnlyList<ServiceError>> _validate;

        public FakeStepExecutor(string stepType, StepOutputSchema? outputSchema = null, Func<StepExecutionContext, CancellationToken, Task<StepExecutionResult>>? execute = null, Func<string, WorkflowValidationContext, IReadOnlyList<ServiceError>>? validate = null)
        {
            StepType = stepType;
            OutputSchema = outputSchema ?? StepOutputSchema.Any;
            _execute = execute ?? ((_, _) => Task.FromResult(StepExecutionResult.Success()));
            _validate = validate ?? ((_, _) => []);
        }

        public string StepType { get; }
        public StepOutputSchema OutputSchema { get; }

        public IReadOnlyList<ServiceError> ValidateConfiguration(string configJson, WorkflowValidationContext context) => _validate(configJson, context);

        public Task<StepExecutionResult> ExecuteAsync(StepExecutionContext context, CancellationToken cancellationToken) => _execute(context, cancellationToken);
    }
}
