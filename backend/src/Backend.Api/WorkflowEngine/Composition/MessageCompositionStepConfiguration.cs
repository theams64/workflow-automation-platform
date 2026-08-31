namespace Backend.Api.WorkflowEngine.Composition
{
    public sealed class MessageCompositionStepConfiguration
    {
        // Simple mode 
        public string? Template { get; init; }

        // Collection mode
        public string? TitleTemplate { get; init; }
        public string EmptyMessageTemplate { get; init; } = "No items were found.";
        public string CollectionReference { get; init; } = string.Empty;
        public string ItemTemplate { get; init; } = string.Empty;
        public int MaximumItems { get; init; } = 10;
        public string Separator { get; init; } = "\n";
    }
}