using Backend.Api.Services.Common;
using Backend.Api.WorkflowEngine.Abstractions;
using Backend.Api.WorkflowEngine.Execution;
using Backend.Api.WorkflowEngine.Expressions;
using Backend.Api.WorkflowEngine.Validation;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Backend.Api.WorkflowEngine.Transform
{
    public sealed class TransformStepExecutor(TransformTemplateProcessor processor) : IWorkflowStepExecutor
    {
        public const string StepTypeName = "transform";

        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            MaxDepth = 32
        };

        public string StepType => StepTypeName;

        public StepOutputSchema OutputSchema { get; } = new(new HashSet<string>(StringComparer.Ordinal));

        public IReadOnlyList<ServiceError> ValidateConfiguration(string configJson, WorkflowValidationContext context)
        {
            TransformStepConfiguration? configuration;

            try
            {
                configuration = JsonSerializer.Deserialize<TransformStepConfiguration>(configJson, SerializerOptions);
            }
            catch (JsonException)
            {
                return
                [
                    new("workflow_step.config_invalid", $"Step {context.CurrentStepOrder}: transform configuration is invalid.")
                ];
            }

            if (configuration is null)
            {
                return
                [
                    new("workflow_step.config_invalid", $"Step {context.CurrentStepOrder}: transform configuration is invalid.")
                ];
            }

            return processor.Validate(configuration, context);
        }

        public StepOutputSchema GetOutputSchema(string configJson)
        {
            try
            {
                var configuration = JsonSerializer.Deserialize<TransformStepConfiguration>(configJson, SerializerOptions);

                return configuration is null ? OutputSchema : processor.DeriveSchema(configuration);
            }
            catch (JsonException)
            {
                return OutputSchema;
            }
        }

        public Task<StepExecutionResult> ExecuteAsync(StepExecutionContext context, CancellationToken cancellationToken)
        {
            try
            {
                var configuration = JsonSerializer.Deserialize<TransformStepConfiguration>(context.Step.ConfigJson, SerializerOptions) ?? throw new TransformProcessingException("The transform configuration is invalid.");

                var output = processor.Render(configuration, context.WorkflowContext, cancellationToken);

                return Task.FromResult(StepExecutionResult.Success(NormalizedStepOutput.FromValue(output)));
            }
            catch (WorkflowExpressionException exception)
            {
                return Task.FromResult(StepExecutionResult.Failure(
                    exception.Code == ExecutionErrorCodes.InvalidReference ? ExecutionErrorCodes.InvalidReference : ExecutionErrorCodes.TransformExecutionFailed,
                    exception.Code == ExecutionErrorCodes.InvalidReference ? exception.SafeMessage : "The transform could not be evaluated."));
            }
            catch (TransformProcessingException)
            {
                return Task.FromResult(StepExecutionResult.Failure(ExecutionErrorCodes.TransformExecutionFailed, "The transform could not be evaluated."));
            }
            catch (JsonException)
            {
                return Task.FromResult(StepExecutionResult.Failure(ExecutionErrorCodes.InvalidStepConfiguration, "The transform configuration is invalid."));
            }
        }
    }
}
