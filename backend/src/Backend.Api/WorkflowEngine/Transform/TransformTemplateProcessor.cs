using Backend.Api.Configuration;
using Backend.Api.Services.Common;
using Backend.Api.WorkflowEngine.Execution;
using Backend.Api.WorkflowEngine.Expressions;
using Backend.Api.WorkflowEngine.Validation;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Backend.Api.WorkflowEngine.Transform
{
    public sealed class TransformTemplateProcessor
    {
        private readonly WorkflowExpressionEngine _expressions;
        private readonly WorkflowExpressionOptions _expressionOptions;
        private readonly TransformOptions _options;

        public TransformTemplateProcessor(WorkflowExpressionEngine expressions, IOptions<WorkflowExpressionOptions> expressionOptions, IOptions<TransformOptions> options)
        {
            _expressions = expressions;
            _expressionOptions = expressionOptions.Value;
            _options = options.Value;
        }

        public IReadOnlyList<ServiceError> Validate(TransformStepConfiguration configuration, WorkflowValidationContext context)
        {
            var errors = new List<ServiceError>();

            if (configuration.Output.ValueKind != JsonValueKind.Object)
            {
                errors.Add(new("workflow_step.transform_output_required", $"Step {context.CurrentStepOrder}: transform output must be a JSON object."));
                return errors;
            }

            ValidateNode(configuration.Output, context, allowItemReferences: false, depth: 1, mapDepth: 0, errors);

            return errors;
        }

        public JsonElement Render(TransformStepConfiguration configuration, WorkflowExecutionContext workflowContext, CancellationToken cancellationToken)
        {
            if (configuration.Output.ValueKind != JsonValueKind.Object)
            {
                throw new TransformProcessingException("The transform output configuration is invalid.");
            }

            var budget = new ExpressionBudget(_expressionOptions.MaxOperations, cancellationToken);

            var rendered = RenderNode(configuration.Output, new ExpressionEvaluationContext(workflowContext), budget, depth: 1, mapDepth: 0);

            return JsonSerializer.SerializeToElement(rendered);
        }

        public StepOutputSchema DeriveSchema(TransformStepConfiguration configuration)
        {
            if (configuration.Output.ValueKind != JsonValueKind.Object)
            {
                return new StepOutputSchema(new HashSet<string>(StringComparer.Ordinal));
            }

            var paths = new HashSet<string>(StringComparer.Ordinal);
            CollectSchemaPaths(configuration.Output, null, paths);
            return new StepOutputSchema(paths);
        }

        private void ValidateNode(JsonElement node, WorkflowValidationContext context, bool allowItemReferences, int depth, int mapDepth, ICollection<ServiceError> errors)
        {
            if (depth > _options.MaxOutputDepth)
            {
                errors.Add(new("workflow_step.transform_depth_exceeded", $"Step {context.CurrentStepOrder}: transform output nesting is too deep."));
                return;
            }

            switch (node.ValueKind)
            {
                case JsonValueKind.Object:
                    {
                        if (TryGetMap(node, out var map))
                        {
                            ValidateMap(map, context, allowItemReferences, depth, mapDepth, errors);
                            return;
                        }

                        var properties = node.EnumerateObject().ToArray();

                        if (properties.Length > _options.MaxPropertiesPerObject)
                        {
                            errors.Add(new("workflow_step.transform_property_limit", $"Step {context.CurrentStepOrder}: a transform object contains too many properties."));
                            return;
                        }

                        foreach (var property in properties)
                        {
                            if (property.Name.StartsWith('$'))
                            {
                                errors.Add(new("workflow_step.transform_directive_invalid", $"Step {context.CurrentStepOrder}: the transform contains an unsupported directive."));
                                continue;
                            }

                            ValidateNode(property.Value, context, allowItemReferences, depth + 1, mapDepth, errors);
                        }

                        return;
                    }

                case JsonValueKind.Array:
                    {
                        if (node.GetArrayLength() > _options.MaxStaticArrayItems)
                        {
                            errors.Add(new("workflow_step.transform_collection_limit", $"Step {context.CurrentStepOrder}: a transform array contains too many configured items."));
                            return;
                        }

                        foreach (var item in node.EnumerateArray())
                        {
                            ValidateNode(item, context, allowItemReferences, depth + 1, mapDepth, errors);
                        }

                        return;
                    }

                case JsonValueKind.String:
                    errors.AddRange(_expressions.ValidateTemplate(node.GetString() ?? string.Empty, context, allowItemReferences, "workflow_step.transform_expression_invalid"));
                    return;

                case JsonValueKind.Number:
                case JsonValueKind.True:
                case JsonValueKind.False:
                case JsonValueKind.Null:
                    return;

                default:
                    errors.Add(new("workflow_step.transform_value_invalid", $"Step {context.CurrentStepOrder}: the transform contains an unsupported value."));
                    return;
            }
        }

        private void ValidateMap(JsonElement map, WorkflowValidationContext context, bool allowItemReferences, int depth, int mapDepth, ICollection<ServiceError> errors)
        {
            if (mapDepth >= 1)
            {
                errors.Add(new("workflow_step.transform_nested_map_invalid", $"Step {context.CurrentStepOrder}: nested transform maps are not supported."));
                return;
            }

            if (map.ValueKind != JsonValueKind.Object)
            {
                errors.Add(new("workflow_step.transform_map_invalid", $"Step {context.CurrentStepOrder}: a transform map is invalid."));
                return;
            }

            var mapProperties = map.EnumerateObject().ToArray();
            var allowed = new HashSet<string>(["source", "maximumItems", "item"], StringComparer.Ordinal);

            if (mapProperties.Any(property => !allowed.Contains(property.Name)))
            {
                errors.Add(new("workflow_step.transform_map_invalid", $"Step {context.CurrentStepOrder}: a transform map contains an unsupported field."));
            }

            if (!map.TryGetProperty("source", out var source) || source.ValueKind != JsonValueKind.String)
            {
                errors.Add(new("workflow_step.transform_map_source_required", $"Step {context.CurrentStepOrder}: a transform map requires a source expression."));
            }
            else
            {
                var sourceTemplate = source.GetString() ?? string.Empty;

                if (!_expressions.TryParseCompleteReferenceTemplate(sourceTemplate, out _))
                {
                    errors.Add(new("workflow_step.transform_map_source_invalid", $"Step {context.CurrentStepOrder}: a transform map source must be a complete reference."));
                }

                errors.AddRange(_expressions.ValidateTemplate(sourceTemplate, context, allowItemReferences, "workflow_step.transform_expression_invalid"));
            }

            if (!map.TryGetProperty("maximumItems", out var maximumItems) ||
                maximumItems.ValueKind != JsonValueKind.Number ||
                !maximumItems.TryGetInt32(out var itemLimit) ||
                itemLimit < 1 ||
                itemLimit > _options.MaxCollectionItems)
            {
                errors.Add(new("workflow_step.transform_map_limit_invalid", $"Step {context.CurrentStepOrder}: a transform map item limit is invalid."));
            }

            if (!map.TryGetProperty("item", out var itemTemplate))
            {
                errors.Add(new("workflow_step.transform_map_item_required", $"Step {context.CurrentStepOrder}: a transform map requires an item template."));
                return;
            }

            ValidateNode(itemTemplate, context, allowItemReferences: true, depth + 1, mapDepth + 1, errors);
        }

        private JsonNode? RenderNode(JsonElement node, ExpressionEvaluationContext context, ExpressionBudget budget, int depth, int mapDepth)
        {
            budget.Consume();

            if (depth > _options.MaxOutputDepth)
            {
                throw new TransformProcessingException("The transform output exceeded the allowed nesting depth.");
            }

            switch (node.ValueKind)
            {
                case JsonValueKind.Object:
                    if (TryGetMap(node, out var map))
                    {
                        return RenderMap(map, context, budget, depth, mapDepth);
                    }

                    var resultObject = new JsonObject();
                    var propertyCount = 0;

                    foreach (var property in node.EnumerateObject())
                    {
                        propertyCount++;

                        if (propertyCount > _options.MaxPropertiesPerObject)
                        {
                            throw new TransformProcessingException("The transform output contained too many properties.");
                        }

                        if (property.Name.StartsWith('$'))
                        {
                            throw new TransformProcessingException("The transform contains an unsupported directive.");
                        }

                        resultObject[property.Name] = RenderNode(property.Value, context, budget, depth + 1, mapDepth);
                    }

                    return resultObject;

                case JsonValueKind.Array:
                    {
                        if (node.GetArrayLength() > _options.MaxStaticArrayItems)
                        {
                            throw new TransformProcessingException("The transform contains too many configured array items.");
                        }

                        var array = new JsonArray();

                        foreach (var item in node.EnumerateArray())
                        {
                            array.Add(RenderNode(item, context, budget, depth + 1, mapDepth));
                        }

                        return array;
                    }

                case JsonValueKind.String:
                    {
                        var evaluated = _expressions.EvaluateTemplate(node.GetString() ?? string.Empty, context, budget);

                        return JsonNode.Parse(evaluated.GetRawText());
                    }

                case JsonValueKind.Number:
                case JsonValueKind.True:
                case JsonValueKind.False:
                case JsonValueKind.Null:
                    return JsonNode.Parse(node.GetRawText());

                default:
                    throw new TransformProcessingException("The transform contains an unsupported value.");
            }
        }

        private JsonArray RenderMap(JsonElement map, ExpressionEvaluationContext context, ExpressionBudget budget, int depth, int mapDepth)
        {
            if (mapDepth >= 1)
            {
                throw new TransformProcessingException("Nested transform maps are not supported.");
            }

            if (!map.TryGetProperty("source", out var sourceElement) ||
                sourceElement.ValueKind != JsonValueKind.String ||
                !map.TryGetProperty("maximumItems", out var maximumItemsElement) ||
                !maximumItemsElement.TryGetInt32(out var maximumItems) ||
                maximumItems < 1 ||
                maximumItems > _options.MaxCollectionItems ||
                !map.TryGetProperty("item", out var itemTemplate))
            {
                throw new TransformProcessingException("The transform map configuration is invalid.");
            }

            var source = _expressions.EvaluateTemplate(sourceElement.GetString() ?? string.Empty, context, budget);

            if (source.ValueKind != JsonValueKind.Array)
            {
                throw new TransformProcessingException("A transform map source must resolve to an array.");
            }

            var output = new JsonArray();
            var count = 0;

            foreach (var sourceItem in source.EnumerateArray())
            {
                if (count >= maximumItems)
                {
                    break;
                }

                budget.Consume();

                output.Add(RenderNode(itemTemplate, new ExpressionEvaluationContext(context.WorkflowContext, sourceItem.Clone()), budget, depth + 1, mapDepth + 1));

                count++;
            }

            return output;
        }

        private static bool TryGetMap(JsonElement element, out JsonElement map)
        {
            map = default;

            if (element.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            var properties = element.EnumerateObject().ToArray();

            if (properties.Length != 1 || !string.Equals(properties[0].Name, "$map", StringComparison.Ordinal))
            {
                return false;
            }

            map = properties[0].Value;
            return true;
        }

        private static void CollectSchemaPaths(JsonElement node, string? currentPath, ISet<string> paths)
        {
            if (currentPath is not null)
            {
                paths.Add(currentPath);
            }

            if (node.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            if (TryGetMap(node, out var map))
            {
                if (map.ValueKind == JsonValueKind.Object && map.TryGetProperty("item", out var item))
                {
                    CollectSchemaPaths(item, currentPath, paths);
                }

                return;
            }

            foreach (var property in node.EnumerateObject())
            {
                var childPath = currentPath is null ? property.Name : $"{currentPath}.{property.Name}";

                CollectSchemaPaths(property.Value, childPath, paths);
            }
        }
    }
}
