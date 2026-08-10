namespace Backend.Api.WorkflowEngine.Validation
{
    public sealed record StepOutputSchema(IReadOnlySet<string> KnownPaths, bool AllowAdditionalPaths = false)
    {
        public static StepOutputSchema Any { get; } = new(new HashSet<string>(StringComparer.Ordinal), true);

        public bool Supports(string path) => AllowAdditionalPaths || KnownPaths.Contains(path);
    }
}
