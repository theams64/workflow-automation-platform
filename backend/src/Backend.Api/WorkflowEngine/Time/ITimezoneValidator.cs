namespace Backend.Api.WorkflowEngine.Time
{
    public interface ITimezoneValidator
    {
        bool IsValid(string timezone);
        TimeZoneInfo GetRequired(string tiemzone);
    }
}
