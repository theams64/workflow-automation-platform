using Backend.Api.Models.Entities;
using Backend.Api.WorkflowEngine.Validation;

namespace Backend.Api.WorkflowEngine.Abstractions
{
    public interface IWorkflowValidationService
    {
        WorkflowValidationResult Validate(IReadOnlyList<WorkflowStep> orderedSteps, WorkflowValidationMode mode);
    }
}
