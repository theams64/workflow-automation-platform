using Backend.Api.Configuration;
using Backend.Api.Models.Validation;
using Backend.Api.Services.Common;
using Backend.Api.WorkflowEngine.Abstractions;
using Backend.Api.WorkflowEngine.Execution;
using Backend.Api.WorkflowEngine.Expressions;
using Backend.Api.WorkflowEngine.References;
using Backend.Api.WorkflowEngine.Validation;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Backend.Api.WorkflowEngine.Composition
{
    public sealed class MessageCompositionStepExecutor(WorkflowExpressionEngine expressions, IOptions<WorkflowExpressionOptions> expressionOptions, IOptions<MessageCompositionOptions> options) : IWorkflowStepExecutor
    {
        public const string StepTypeName = "message.compose";

        private readonly WorkflowExpressionOptions _expressionOptions = expressionOptions.Value;
        private readonly MessageCompositionOptions _options = options.Value;

        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            MaxDepth = 16
        };

        public string StepType => StepTypeName;

        public StepOutputSchema OutputSchema { get; } = new(new HashSet<string>(["text"], StringComparer.Ordinal));

        public StepOutputSchema GetOutputSchema(string configJson) => OutputSchema;

        public IReadOnlyList<ServiceError> ValidateConfiguration(string configJson, WorkflowValidationContext context)
        {
            MessageCompositionStepConfiguration? configuration;

            try
            {
                configuration = JsonSerializer.Deserialize<MessageCompositionStepConfiguration>(configJson, SerializerOptions);
            }
            catch (JsonException)
            {
                return
                [
                    new("workflow_step.config_invalid", $"Step {context.CurrentStepOrder}: message composition configuration is invalid.")
                ];
            }

            if (configuration is null)
            {
                return
                [
                    new("workflow_step.config_invalid", $"Step {context.CurrentStepOrder}: message composition configuration is invalid.")
                ];
            }

            var errors = new List<ServiceError>();
            var collectionMode = !string.IsNullOrWhiteSpace(configuration.CollectionReference);

            if (!collectionMode)
            {
                if (configuration.Template is null)
                {
                    errors.Add(new("workflow_step.message_template_required", $"Step {context.CurrentStepOrder}: a message template is required when no collection is configured."));
                }
                else
                {
                    errors.AddRange(expressions.ValidateTemplate(configuration.Template, context, allowItemReferences: false, "workflow_step.message_expression_invalid"));
                }

                if (!string.IsNullOrWhiteSpace(configuration.ItemTemplate))
                {
                    errors.Add(new("workflow_step.message_collection_invalid", $"Step {context.CurrentStepOrder}: an item template requires a collection reference."));
                }

                return errors;
            }

            if (configuration.Template is not null)
            {
                errors.Add(new("workflow_step.message_mode_invalid", $"Step {context.CurrentStepOrder}: use either the simple template or collection mode, not both."));
            }

            if (configuration.MaximumItems < 1 || configuration.MaximumItems > _options.MaxItems)
            {
                errors.Add(new("workflow_step.message_item_limit_invalid", $"Step {context.CurrentStepOrder}: the message item limit is invalid."));
            }

            if (Encoding.UTF8.GetByteCount(configuration.Separator ?? string.Empty) > _options.MaxSeparatorBytes || !IsSafeSeparator(configuration.Separator ?? string.Empty))
            {
                errors.Add(new("workflow_step.message_separator_invalid", $"Step {context.CurrentStepOrder}: the message separator is invalid."));
            }

            if (configuration.ItemTemplate is null)
            {
                errors.Add(new("workflow_step.message_item_template_required", $"Step {context.CurrentStepOrder}: an item template is required when a collection is configured."));
            }

            if (!expressions.TryParseCompleteReferenceTemplate(configuration.CollectionReference!, out var collectionReference) || collectionReference.Scope == WorkflowReferenceScope.CurrentItem)
            {
                errors.Add(new("workflow_step.message_collection_invalid", $"Step {context.CurrentStepOrder}: the message collection must be a complete workflow, execution, or prior-step reference."));
            }

            errors.AddRange(expressions.ValidateTemplate(configuration.CollectionReference!, context, allowItemReferences: false, "workflow_step.message_expression_invalid"));

            if (configuration.ItemTemplate is not null)
            {
                errors.AddRange(expressions.ValidateTemplate(configuration.ItemTemplate, context, allowItemReferences: true, "workflow_step.message_expression_invalid"));
            }

            errors.AddRange(expressions.ValidateTemplate(configuration.EmptyMessageTemplate, context, allowItemReferences: false, "workflow_step.message_expression_invalid"));

            if (configuration.TitleTemplate is not null)
            {
                errors.AddRange(expressions.ValidateTemplate(configuration.TitleTemplate, context, allowItemReferences: false, "workflow_step.message_expression_invalid"));
            }

            return errors;
        }

        public Task<StepExecutionResult> ExecuteAsync(StepExecutionContext context, CancellationToken cancellationToken)
        {
            try
            {
                var configuration = JsonSerializer.Deserialize<MessageCompositionStepConfiguration>(context.Step.ConfigJson, SerializerOptions)
                    ?? throw new WorkflowExpressionException("message_configuration_invalid", "The message composition configuration is invalid.");

                var budget = new ExpressionBudget(_expressionOptions.MaxOperations, cancellationToken);

                var evaluationContext = new ExpressionEvaluationContext(context.WorkflowContext);

                var collectionMode = !string.IsNullOrWhiteSpace(configuration.CollectionReference);

                string text;

                if (!collectionMode)
                {
                    if (configuration.Template is null)
                    {
                        return Task.FromResult(StepExecutionResult.Failure(ExecutionErrorCodes.InvalidStepConfiguration, "The message composition configuration is invalid."));
                    }

                    text = expressions.RenderTextTemplate(configuration.Template, evaluationContext, budget);
                }
                else
                {
                    if (configuration.Template is not null || configuration.ItemTemplate is null ||
                        configuration.MaximumItems < 1 || configuration.MaximumItems > _options.MaxItems ||
                        Encoding.UTF8.GetByteCount(configuration.Separator ?? string.Empty) > _options.MaxSeparatorBytes ||
                        !IsSafeSeparator(configuration.Separator ?? string.Empty))
                    {
                        return Task.FromResult(StepExecutionResult.Failure(ExecutionErrorCodes.InvalidStepConfiguration, "The message composition configuration is invalid."));
                    }

                    var collection = expressions.EvaluateTemplate(configuration.CollectionReference!, evaluationContext, budget);

                    if (collection.ValueKind != JsonValueKind.Array)
                    {
                        return Task.FromResult(StepExecutionResult.Failure(ExecutionErrorCodes.TemplateValidationFailed, "The configured message collection did not resolve to an array."));
                    }

                    var title = configuration.TitleTemplate is null ? string.Empty : expressions.RenderTextTemplate(configuration.TitleTemplate, evaluationContext, budget);

                    var lines = new List<string>();
                    var itemCount = 0;

                    foreach (var item in collection.EnumerateArray())
                    {
                        if (itemCount >= configuration.MaximumItems)
                        {
                            break;
                        }

                        budget.Consume();

                        lines.Add(expressions.RenderTextTemplate(configuration.ItemTemplate, new ExpressionEvaluationContext(context.WorkflowContext, item.Clone()), budget));

                        itemCount++;
                    }

                    string body;

                    if (lines.Count == 0)
                    {
                        body = expressions.RenderTextTemplate(configuration.EmptyMessageTemplate, evaluationContext, budget);
                    }
                    else
                    {
                        body = string.Join(configuration.Separator, lines);
                    }

                    text = string.IsNullOrWhiteSpace(title) ? body : string.IsNullOrEmpty(body) ? title : $"{title}\n\n{body}";
                }

                if (Encoding.UTF8.GetByteCount(text) > _options.MaxMessageBytes)
                {
                    return Task.FromResult(StepExecutionResult.Failure(ExecutionErrorCodes.MessageSizeExceeded, "The composed message exceeded the allowed size."));
                }

                var output = NormalizedStepOutput.FromValue(new { text });
                return Task.FromResult(StepExecutionResult.Success(output));
            }
            catch (WorkflowExpressionException exception)
            {
                return Task.FromResult(StepExecutionResult.Failure(
                    exception.Code == ExecutionErrorCodes.InvalidReference ? ExecutionErrorCodes.InvalidReference : ExecutionErrorCodes.TemplateValidationFailed,
                    exception.Code == ExecutionErrorCodes.InvalidReference ? exception.SafeMessage : "The message template could not be evaluated."));
            }
            catch (JsonException)
            {
                return Task.FromResult(StepExecutionResult.Failure(ExecutionErrorCodes.InvalidStepConfiguration, "The message composition configuration is invalid."));
            }
        }

        private static bool IsSafeSeparator(string value) => value.All(character => !char.IsControl(character) || character is '\r' or '\n' or '\t');
    }
}
