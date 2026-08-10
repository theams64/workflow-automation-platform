namespace Backend.Api.WorkflowEngine.Execution
{
    public static class ExecutionErrorCodes
    {
        public const string InvalidWorkflow = "invalid_workflow";
        public const string InvalidStepConfiguration = "invalid_step_configuration";
        public const string InvalidReference = "invalid_reference";
        public const string UnknownStepType = "unknown_step_type";
        public const string StepFailed = "step_failed";
        public const string StepTimeout = "step_timeout";
        public const string WorkflowTimeout = "workflow_timeout";
        public const string WorkflowCancelled = "workflow_cancelled";
        public const string OutputTooLarge = "output_too_large";
    }
}
