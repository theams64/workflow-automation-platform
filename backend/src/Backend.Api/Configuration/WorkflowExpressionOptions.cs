using Backend.Api.Models.Validation;
using System.ComponentModel.DataAnnotations;

namespace Backend.Api.Configuration
{
    public sealed class WorkflowExpressionOptions
    {
        public const string SectionName = "WorkflowExpressions";

        [Range(1, WorkflowLimits.ExpressionMaximumDepth)]
        public int MaxDepth { get; init; } = 8;

        [Range(1, WorkflowLimits.ExpressionMaximumOperations)]
        public int MaxOperations { get; init; } = 2_000;

        [Range(16, WorkflowLimits.ExpressionMaximumTemplateBytes)]
        public int MaxTemplateBytes { get; init; } = 8 * 1024;

        [Range(16, WorkflowLimits.ExpressionMaximumStringBytes)]
        public int MaxStringBytes { get; init; } = 16 * 1024;

        [Range(1, WorkflowLimits.ExpressionMaximumFunctionArguments)]
        public int MaxFunctionArguments { get; init; } = 6;
    }
}