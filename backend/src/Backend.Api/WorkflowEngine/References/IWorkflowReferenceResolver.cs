using Backend.Api.WorkflowEngine.Execution;
using System.Text.Json;

namespace Backend.Api.WorkflowEngine.References
{
    public interface IWorkflowReferenceResolver
    {
        bool TryResolve(WorkflowReference reference, WorkflowExecutionContext context, out JsonElement value);
    }
}
