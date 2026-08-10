namespace Backend.Api.WorkflowEngine.Execution
{
    public sealed record StepExecutionResult(bool Succeeded, NormalizedStepOutput? Output, string? ErrorCode, string? ErrorMessage)
    {
        public static StepExecutionResult Success(NormalizedStepOutput? output = null) => new(true, output ?? NormalizedStepOutput.Empty, null, null);

        public static StepExecutionResult Failure(string code, string safeMessage) => new(false, null, code, safeMessage);
    }
}
