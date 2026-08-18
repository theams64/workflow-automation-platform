namespace Backend.Api.WorkflowEngine.Validation
{
    public sealed record StepOutputSchema(IReadOnlySet<string> KnownPaths, bool AllowAdditionalPaths = false, IReadOnlySet<string>? AdditionalPathPrefixes = null)
    {
        public static StepOutputSchema Any { get; } = new(new HashSet<string>(StringComparer.Ordinal), true);

        public bool Supports(string path)
        {
            if (AllowAdditionalPaths || KnownPaths.Contains(path))
            {
                return true;
            }

            if (AdditionalPathPrefixes is null)
            {
                return false;
            }

            return AdditionalPathPrefixes.Any(prefix => path.Length > prefix.Length && path.StartsWith(prefix, StringComparison.Ordinal) && path[prefix.Length] == '.');
        }
    }
}
