using Backend.Api.Configuration;
using Backend.Api.Models.Validation;
using Backend.Api.Services.Common;
using Backend.Api.WorkflowEngine.Abstractions;
using Backend.Api.WorkflowEngine.Connections;
using Backend.Api.WorkflowEngine.Execution;
using Backend.Api.WorkflowEngine.Expressions;
using Backend.Api.WorkflowEngine.Validation;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Backend.Api.WorkflowEngine.Slack
{
    public sealed class SlackNotificationStepExecutor(IManagedConnectionRuntimeResolver connectionResolver, IConnectionSecretProvider secretProvider, WorkflowExpressionEngine expressions, ISlackWebhookClient slackClient, IOptions<SlackDeliveryOptions> options) : IWorkflowStepExecutor
    {
        public const string StepTypeName = "slack.notification";

        private readonly SlackDeliveryOptions _options = options.Value;

        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            MaxDepth = 0
        };

        public string StepType => StepTypeName;

        public StepOutputSchema OutputSchema { get; } = new(new HashSet<string>(["delivered"], StringComparer.Ordinal));

        public StepOutputSchema GetOutputSchema(string configJson) => OutputSchema;

        public IReadOnlyList<ServiceError> ValidateConfiguration(string configJson, WorkflowValidationContext context)
        {
            SlackNotificationStepConfiguration? configuration;

            try
            {
                configuration = JsonSerializer.Deserialize<SlackNotificationStepConfiguration>(configJson, SerializerOptions);
            }
            catch (JsonException)
            {
                return
                [
                    new("workflow_step.config_invalid", $"Step {context.CurrentStepOrder}: Slack notification configuration is invalid.")
                ];
            }

            if (configuration is null || configuration.ConnectionId == Guid.Empty)
            {
                return
                [
                    new("workflow_step.slack_connection_required", $"Step {context.CurrentStepOrder}: a Slack managed connection is required.")
                ];
            }

            if (string.IsNullOrEmpty(configuration.Message))
            {
                return
                [
                    new("workflow_step.slack_message_required", $"Step {context.CurrentStepOrder}: a Slack message is required.")
                ];
            }

            return expressions.ValidateTemplate(configuration.Message, context, allowItemReferences: false, "workflow_step.slack_message_invalid");
        }

        public async Task<StepExecutionResult> ExecuteAsync(StepExecutionContext context, CancellationToken cancellationToken)
        {
            SlackNotificationStepConfiguration configuration;

            try
            {
                configuration = JsonSerializer.Deserialize<SlackNotificationStepConfiguration>(context.Step.ConfigJson, SerializerOptions) ?? throw new JsonException();
            }
            catch (JsonException)
            {
                return StepExecutionResult.Failure(ExecutionErrorCodes.InvalidStepConfiguration, "The Slack notification configuration is invalid.");
            }

            if (configuration.ConnectionId == Guid.Empty || string.IsNullOrEmpty(configuration.Message))
            {
                return StepExecutionResult.Failure(ExecutionErrorCodes.InvalidStepConfiguration, "The Slack notification configuration is invalid.");
            }

            string text;

            try
            {
                var budget = new ExpressionBudget(WorkflowLimits.ExpressionMaximumOperations, cancellationToken);

                text = expressions.RenderTextTemplate(configuration.Message, new ExpressionEvaluationContext(context.WorkflowContext), budget);
            }
            catch (WorkflowExpressionException exception)
            {
                return StepExecutionResult.Failure(
                    exception.Code == ExecutionErrorCodes.InvalidReference ? ExecutionErrorCodes.InvalidReference : ExecutionErrorCodes.TemplateValidationFailed,
                    exception.Code == ExecutionErrorCodes.InvalidReference ? exception.SafeMessage : "The Slack message template could not be evaluated.");
            }

            if (Encoding.UTF8.GetByteCount(text) > _options.MaxMessageBytes)
            {
                return StepExecutionResult.Failure(ExecutionErrorCodes.MessageSizeExceeded, "The Slack message exceeded the allowed size.");
            }

            var connection = await connectionResolver.GetOwnedAsync(configuration.ConnectionId, context.Workflow.UserID, cancellationToken);

            if (connection is null ||
                !string.Equals(connection.ConnectionType, ManagedConnectionTypes.SlackWebhook, StringComparison.Ordinal) ||
                !string.Equals(connection.CredentialType, ManagedCredentialTypes.SlackWebhookUrl, StringComparison.Ordinal) ||
                !string.Equals(connection.CredentialPlacement, ManagedCredentialPlacements.Uri, StringComparison.Ordinal))
            {
                return StepExecutionResult.Failure(ExecutionErrorCodes.InvalidConnection, "The Slack managed connection is invalid.");
            }

            if (connection.RevokedAt is not null)
            {
                return StepExecutionResult.Failure(ExecutionErrorCodes.ConnectionRevoked, "The Slack managed connection has been revoked.");
            }

            if (!connection.IsEnabled)
            {
                return StepExecutionResult.Failure(ExecutionErrorCodes.ConnectionDisabled, "The Slack managed connection is disabled.");
            }

            ConnectionSecretMaterial secret;

            try
            {
                secret = await secretProvider.GetSecretAsync(connection.SecretReference, cancellationToken);
            }
            catch (ConnectionSecretException)
            {
                return StepExecutionResult.Failure(ExecutionErrorCodes.ConnectionSecretUnavailable, "The Slack managed connection credential is unavailable.");
            }

            if (!SlackWebhookUriPolicy.TryCreate(secret.Value, connection.CanonicalOrigin, out var webhookUri))
            {
                return StepExecutionResult.Failure(ExecutionErrorCodes.InvalidConnection, "The Slack managed connection is invalid.");
            }

            try
            {
                await slackClient.SendAsync(new SlackWebhookRequest(context.Workflow.UserID, context.Workflow.ID, connection.ID, webhookUri, text), cancellationToken);
            }
            catch (SlackDeliveryException exception)
            {
                return StepExecutionResult.Failure(exception.Code, exception.SafeMessage);
            }

            return StepExecutionResult.Success(NormalizedStepOutput.FromValue(new { delivered = true }));
        }
    }
}
