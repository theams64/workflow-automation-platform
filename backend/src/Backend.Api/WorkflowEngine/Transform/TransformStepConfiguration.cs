using System.Text.Json;

namespace Backend.Api.WorkflowEngine.Transform
{
    public sealed class TransformStepConfiguration
    {
        public JsonElement Output { get; init; }
    }
}
