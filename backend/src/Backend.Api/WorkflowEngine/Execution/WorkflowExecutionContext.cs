using System.Collections.ObjectModel;
using System.Text.Json;

namespace Backend.Api.WorkflowEngine.Execution
{
    public sealed class WorkflowExecutionContext
    {
        private readonly Dictionary<string, NormalizedStepOutput> _outputs = new(StringComparer.Ordinal);

        public WorkflowExecutionContext(WorkflowExecutionMetadata execution, JsonElement workflowInputs)
        {
            Execution = execution;
            WorkflowInputs = workflowInputs.Clone();
        }

        public WorkflowExecutionMetadata Execution { get; }
        public JsonElement WorkflowInputs { get; }

        public IReadOnlyDictionary<string, NormalizedStepOutput> StepOutputs => new ReadOnlyDictionary<string, NormalizedStepOutput>(_outputs);

        public void AddStepOutput(string stepKey, NormalizedStepOutput output)
        {
            if (!_outputs.TryAdd(stepKey, output))
            {
                throw new InvalidOperationException($"Output for step key '{stepKey}' already exists.");
            }
        }
    }
}
