namespace Backend.Api.WorkflowEngine.Validation
{
    public sealed record WorkflowValidationContext(int CurrentStepOrder, IReadOnlyDictionary<string, StepOutputSchema> PriorStepSchemas);
}
