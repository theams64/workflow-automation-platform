using Backend.Api.WorkflowEngine.Execution;
using System.Globalization;
using System.Text.Json;

namespace Backend.Api.WorkflowEngine.References
{
    public sealed class WorkflowReferenceResolver : IWorkflowReferenceResolver
    {
        public bool TryResolve(WorkflowReference reference, WorkflowExecutionContext context, out JsonElement value)
        {
            switch (reference.Scope)
            {
                case WorkflowReferenceScope.WorkflowInputs:
                    return TryResolvePath(context.WorkflowInputs, reference.Path, out value);

                case WorkflowReferenceScope.StepOutput:
                    if (reference.StepKey is null || !context.StepOutputs.TryGetValue(reference.StepKey, out var output))
                    {
                        value = default;
                        return false;
                    }
                    
                    return TryResolvePath(output.Value, reference.Path, out value);

                case WorkflowReferenceScope.Execution:
                    return TryResolveExecution(context.Execution, reference.Path, out value);

                case WorkflowReferenceScope.CurrentItem:
                default:
                    value = default;
                    return false;
            }
        }

        private static bool TryResolveExecution(WorkflowExecutionMetadata execution, string path, out JsonElement value)
        {
            object? result = path switch
            {
                "executionId" => execution.ExecutionId,
                "triggerType" => execution.TriggerType,
                "startedAt" => execution.StartedAt,
                "scheduledFor" => execution.ScheduledFor,
                "effectiveDate" => execution.EffectiveDate,
                "timezone" => execution.Timezone,
                _ => null
            };

            if (result is null && path != "scheduledFor")
            {
                value = default;
                return false;
            }

            value = JsonSerializer.SerializeToElement(result);
            return true;
        }

        private static bool TryResolvePath(JsonElement root, string path, out JsonElement value)
        {
            var current = root;

            foreach (var segment in path.Split('.', StringSplitOptions.None))
            {
                if (current.ValueKind == JsonValueKind.Object)
                {
                    if (!current.TryGetProperty(segment, out var propertyValue))
                    {
                        value = default;
                        return false;
                    }

                    current = propertyValue;
                    continue;
                }

                if (current.ValueKind == JsonValueKind.Array && int.TryParse(segment, NumberStyles.None, CultureInfo.InvariantCulture, out var index) && index >= 0 && index < current.GetArrayLength())
                {
                    current = current[index];
                    continue;
                }

                value = default;
                return false;
            }

            value = current.Clone();
            return true;
        }
    }
}
