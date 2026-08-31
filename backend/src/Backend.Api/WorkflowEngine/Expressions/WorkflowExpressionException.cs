using Backend.Api.WorkflowEngine.Execution;
using System.Text.Json;

namespace Backend.Api.WorkflowEngine.Expressions
{
    public sealed class WorkflowExpressionException : Exception
    {
        public WorkflowExpressionException(string code, string safeMessage) : base(safeMessage)
        {
            Code = code;
            SafeMessage = safeMessage;
        }

        public string Code { get; }
        public string SafeMessage { get; }
    }
}