namespace Backend.Api.Models.Validation
{
    public static class WorkflowLimits
    {
        public const int NameMaxLength = 200;
        public const int TriggerTypeMaxLength = 50;
        public const int CronExpressionMaxLength = 100;

        public const int StepTypeMaxLength = 100;
        public const int ConfigJsonMaxLength = 32 * 1024;
        public const int MaxStepsPerWorkflow = 50;

        public const int DefaultPageSize = 20;
        public const int MaxPageSize = 100;
    }
}