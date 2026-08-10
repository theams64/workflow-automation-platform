namespace Backend.Api.WorkflowEngine.References
{
    public interface IWorkflowReferenceParser
    {
        bool TryParse(string expression, out WorkflowReference reference);
        IReadOnlyList<WorkflowReference> FindReferences(string configJson);
    }
}
