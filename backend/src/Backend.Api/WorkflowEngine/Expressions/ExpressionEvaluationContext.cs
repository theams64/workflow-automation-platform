using Backend.Api.WorkflowEngine.Execution;
using System.Text.Json;

namespace Backend.Api.WorkflowEngine.Expressions
{
    public sealed record ExpressionEvaluationContext(WorkflowExecutionContext WorkflowContext, JsonElement? CurrentItem = null);
}