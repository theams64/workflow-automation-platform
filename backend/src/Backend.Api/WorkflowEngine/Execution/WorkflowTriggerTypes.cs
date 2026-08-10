namespace Backend.Api.WorkflowEngine.Execution
{
    public static class WorkflowTriggerTypes
    {
        public const string Manual = "manual";
        public const string Schedule = "schedule";

        public static bool IsSupported(string value) => value is Manual or Schedule;
    }
}
