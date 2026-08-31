using Backend.Api.Configuration;
using Backend.Api.Services.Common;
using Backend.Api.WorkflowEngine.Execution;
using Backend.Api.WorkflowEngine.Expressions;
using Backend.Api.WorkflowEngine.Validation;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Backend.Api.WorkflowEngine.Transform
{
    public sealed class TransformProcessingException : Exception
    {
        public TransformProcessingException(string safeMessage) : base(safeMessage)
        {
            SafeMessage = safeMessage;
        }

        public string SafeMessage { get; }
    }
}
