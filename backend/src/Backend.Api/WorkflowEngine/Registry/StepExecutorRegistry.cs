using Backend.Api.WorkflowEngine.Abstractions;

namespace Backend.Api.WorkflowEngine.Registry
{
    public sealed class StepExecutorRegistry : IStepExecutorRegistry
    {
        private readonly IReadOnlyDictionary<string, IWorkflowStepExecutor> _executors;

        public StepExecutorRegistry(IEnumerable<IWorkflowStepExecutor> executors)
        {
            var dictionary = new Dictionary<string, IWorkflowStepExecutor>(StringComparer.OrdinalIgnoreCase);

            foreach (var executor in executors)
            {
                var normalized = Normalize(executor.StepType);

                if (!dictionary.TryAdd(normalized, executor))
                {
                    throw new InvalidOperationException($"Duplicate workflow step executor registration for '{normalized}'.");
                }
            }

            _executors = dictionary;
        }

        public IReadOnlyCollection<string> RegisteredStepTypes => _executors.Keys.ToArray();

        public bool TryGet(string stepType, out IWorkflowStepExecutor executor) => _executors.TryGetValue(Normalize(stepType), out executor!);

        public IWorkflowStepExecutor GetRequired(string stepType) => TryGet(stepType, out var executor) ? executor : throw new KeyNotFoundException($"No workflow step executor is registered for '{stepType}'.");

        private static string Normalize(string value)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            return value.Trim().ToLowerInvariant();
        }
    }
}
