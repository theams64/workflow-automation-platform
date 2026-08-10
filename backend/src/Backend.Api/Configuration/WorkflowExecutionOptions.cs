using Backend.Api.Models.Validation;
using System.ComponentModel.DataAnnotations;

namespace Backend.Api.Configuration
{
    public sealed class WorkflowExecutionOptions
    {
        public const string SectionName = "WorkflowExecution";

        [Range(1, WorkflowLimits.MaximumStepTimeoutSeconds)]
        public int StepTimeoutSeconds { get; init; } = WorkflowLimits.DefaultStepTimeoutSeconds;

        [Range(1, WorkflowLimits.MaximumWorkflowTimeoutSeconds)]
        public int WorkflowTimeoutSeconds { get; init; } = WorkflowLimits.DefaultWorkflowTimeoutSeconds;
    }
}
