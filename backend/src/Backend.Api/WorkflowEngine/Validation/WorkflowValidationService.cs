using Backend.Api.Models.Entities;
using Backend.Api.Services.Common;
using Backend.Api.WorkflowEngine.Abstractions;
using Backend.Api.WorkflowEngine.References;
using System.Text.Json;

namespace Backend.Api.WorkflowEngine.Validation
{
    public sealed class WorkflowValidationService(IStepExecutorRegistry registry, IWorkflowReferenceParser referenceParser) : IWorkflowValidationService
    {
        public WorkflowValidationResult Validate(IReadOnlyList<WorkflowStep> orderedSteps, WorkflowValidationMode mode)
        {
            var errors = new List<ServiceError>();

            if (mode == WorkflowValidationMode.Executable && orderedSteps.Count == 0)
            {
                errors.Add(new("workflow_steps.collection_empty", "At least one workflow step is required."));
                return new(errors);
            }

            var seenKeys = new HashSet<string>(StringComparer.Ordinal);
            var priorSchemas = new Dictionary<string, StepOutputSchema>(StringComparer.Ordinal);

            for (var i = 0; i < orderedSteps.Count; i++)
            {
                var step = orderedSteps[i];
                var expectedOrder = i + 1;

                if (step.StepOrder != expectedOrder)
                {
                    errors.Add(new("workflow_steps.order_invalid", "Workflow step ordering is invalid."));
                }

                if (!StepKeyValidator.IsValid(step.StepKey))
                {
                    errors.Add(new("workflow_step.key_invalid", $"Step {expectedOrder}: the step key is invalid."));
                    continue;
                }

                if (!seenKeys.Add(step.StepKey))
                {
                    errors.Add(new("workflow_step.key_duplicate", $"Step {expectedOrder}: the step key is duplicated."));
                    continue;
                }

                if (!registry.TryGet(step.StepType, out var executor))
                {
                    errors.Add(new("workflow_step.type_unregistered", $"Step {expectedOrder}: the step type is not registered."));
                    continue;
                }

                var context = new WorkflowValidationContext(expectedOrder, priorSchemas);
                errors.AddRange(executor.ValidateConfiguration(step.ConfigJson, context));

                IReadOnlyList<WorkflowReference> references;
                try
                {
                    references = referenceParser.FindReferences(step.ConfigJson);
                }
                catch (JsonException)
                {
                    errors.Add(new("workflow_step.config_invalid", $"Step {expectedOrder}: configuration must be valid JSON."));
                    continue;
                }

                foreach (var reference in references)
                {
                    ValidateReference(reference, priorSchemas, expectedOrder, errors);
                }

                priorSchemas.Add(step.StepKey, executor.OutputSchema);
            }

            return new(errors);
        }

        private static void ValidateReference(WorkflowReference reference, IReadOnlyDictionary<string, StepOutputSchema> priorSchemas, int stepNumber, ICollection<ServiceError> errors)
        {
            if (reference.Scope != WorkflowReferenceScope.StepOutput)
            {
                return;
            }

            if (reference.StepKey is null || !priorSchemas.TryGetValue(reference.StepKey, out var schema))
            {
                errors.Add(new("workflow_step.reference_invalid", $"Step {stepNumber}: a reference targets an unknown or later step."));
                return;
            }

            if (!schema.Supports(reference.Path))
            {
                errors.Add(new("workflow_step.reference_unknown_field", $"Step {stepNumber}: a reference targets an unknown output field."));
            }
        }
    }
}
