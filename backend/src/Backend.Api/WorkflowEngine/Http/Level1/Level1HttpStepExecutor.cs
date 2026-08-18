using Backend.Api.Services.Common;
using Backend.Api.WorkflowEngine.Abstractions;
using Backend.Api.WorkflowEngine.Execution;
using Backend.Api.WorkflowEngine.Validation;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Backend.Api.WorkflowEngine.Http.Level1
{
    public sealed class Level1HttpStepExecutor(IApprovedHttpOriginCatalog originCatalog, Level1HttpRequestMaterializer materializer, ISafeOutboundHttpClient safeHttpClient) : IWorkflowStepExecutor
    {
        public const string StepTypeName = "http.level1";

        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            MaxDepth = 16
        };

        public string StepType => StepTypeName;

        public StepOutputSchema OutputSchema { get; } = new(new HashSet<string>(StringComparer.Ordinal)
        {
            "statusCode",
            "contentType",
            "body",
            "selectedHeaders",
            "receivedBytes"
        },
        AllowAdditionalPaths: false,
        AdditionalPathPrefixes: new HashSet<string>(StringComparer.Ordinal)
        {
            "body",
            "selectedHeaders"
        });

        public IReadOnlyList<ServiceError> ValidateConfiguration(string configJson, WorkflowValidationContext context)
        {
            Level1HttpStepConfiguration? configuration;

            try
            {
                configuration = JsonSerializer.Deserialize<Level1HttpStepConfiguration>(configJson, SerializerOptions);
            }
            catch (JsonException)
            {
                return
                [
                    new("workflow_step.config_invalid", $"Step {context.CurrentStepOrder}: Level 1 HTTP configuration is invalid.")
                ];
            }

            if (configuration is null || string.IsNullOrWhiteSpace(configuration.OriginId))
            {
                return
                [
                    new("workflow_step.http_origin_required", $"Step {context.CurrentStepOrder}: an approved HTTP origin is required.")
                ];
            }

            if (!originCatalog.TryGet(configuration.OriginId, out var policy))
            {
                return
                [
                    new("workflow_step.http_origin_invalid", $"Step {context.CurrentStepOrder}: the selected HTTP origin is not registered.")
                ];
            }

            if (!policy.Enabled)
            {
                return
                [
                    new("workflow_step.http_origin_disabled", $"Step {context.CurrentStepOrder}: the selected HTTP origin is disabled.")
                ];
            }

            if (!policy.AllowedMethods.Contains(HttpMethod.Get.Method))
            {
                return
                [
                    new("workflow_step.http_method_not_allowed", $"Step {context.CurrentStepOrder}: GET is not allowed for the selected HTTP origin.")
                ];
            }

            return materializer.ValidateConfiguration(configuration, policy, context.CurrentStepOrder);
        }

        public async Task<StepExecutionResult> ExecuteAsync(StepExecutionContext context, CancellationToken cancellationToken)
        {
            try
            {
                var configuration = JsonSerializer.Deserialize<Level1HttpStepConfiguration>(context.Step.ConfigJson, SerializerOptions)
                    ?? throw new SafeHttpException(ExecutionErrorCodes.InvalidStepConfiguration, "The Level 1 HTTP configuration is invalid.");

                if (!originCatalog.TryGet(configuration.OriginId, out var policy))
                {
                    return StepExecutionResult.Failure(ExecutionErrorCodes.InvalidOrigin, "The selected HTTP origin is invalid.");
                }

                if (!policy.Enabled)
                {
                    return StepExecutionResult.Failure(ExecutionErrorCodes.OriginDisabled, "The selected HTTP origin is disabled.");
                }

                var request = materializer.Materialize(configuration, policy, context);

                var response = await safeHttpClient.SendAsync(request, policy, cancellationToken);

                var output = NormalizedStepOutput.FromValue(new
                {
                    statusCode = response.StatusCode,
                    contentType = response.ContentType,
                    body = response.Body,
                    selectedHeaders = response.SelectedHeaders,
                    receivedBytes = response.ReceivedBytes
                });

                return StepExecutionResult.Success(output);
            }
            catch (SafeHttpException exception)
            {
                return StepExecutionResult.Failure(exception.Code, exception.SafeMessage);
            }
            catch (JsonException)
            {
                return StepExecutionResult.Failure(ExecutionErrorCodes.InvalidStepConfiguration, "The Level 1 HTTP configuration is invalid.");
            }
        }
    }
}
