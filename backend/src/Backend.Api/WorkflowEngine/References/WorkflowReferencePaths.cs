namespace Backend.Api.WorkflowEngine.References
{
    public static class WorkflowReferencePaths
    {
        private static readonly HashSet<string> ExecutionPaths = new(StringComparer.Ordinal)
        {
            "executionId",
            "triggerType",
            "startedAt",
            "scheduledFor",
            "effectiveDate",
            "timezone"
        };

        public static bool IsSupportedExecutionPath(string path) => ExecutionPaths.Contains(path);
    }
}
