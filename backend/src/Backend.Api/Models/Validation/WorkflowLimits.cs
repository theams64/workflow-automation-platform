using Microsoft.EntityFrameworkCore.Metadata.Conventions;

namespace Backend.Api.Models.Validation
{
    public static class WorkflowLimits
    {
        public const int NameMaxLength = 200;
        public const int TriggerTypeMaxLength = 50;
        public const int CronExpressionMaxLength = 100;
        public const int TimezoneMaxLength = 100;

        public const int StepKeyMaxLength = 64;
        public const int StepTypeMaxLength = 100;
        public const int ConfigJsonMaxLength = 32 * 1024;
        public const int MaxStepsPerWorkflow = 50;

        public const int ExecutionStatusMaxLength = 32;
        public const int ExecutionErrorCodeMaxLength = 100;
        public const int ExecutionErrorMessageMaxLength = 500;

        public const int RuntimeInputMaxBytes = 32 * 1024;
        public const int NormalizedOutputMaxBytes = 256 * 1024;

        public const int DefaultStepTimeoutSeconds = 30;
        public const int MaximumStepTimeoutSeconds = 120;
        public const int DefaultWorkflowTimeoutSeconds = 300;
        public const int MaximumWorkflowTimeoutSeconds = 900;

        public const int DefaultPageSize = 20;
        public const int MaxPageSize = 100;
    }
}