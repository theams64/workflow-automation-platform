using Backend.Api.Services.Common;

namespace Backend.Api.WorkflowEngine.Validation
{
    public sealed record WorkflowValidationResult(IReadOnlyList<ServiceError> Errors)
    {
        public bool Succeeded => Errors.Count == 0;
        public static WorkflowValidationResult Success { get; } = new([]);
    }
}
