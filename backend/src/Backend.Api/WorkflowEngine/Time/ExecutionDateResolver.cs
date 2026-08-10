namespace Backend.Api.WorkflowEngine.Time
{
    public sealed class ExecutionDateResolver(IClock clock, ITimezoneValidator timezoneValidator) : IExecutionDateResolver
    {
        public DateOnly Resolve(ExecutionDateRequest request)
        {
            if (request.ExplicitDate is not null)
            {
                return request.ExplicitDate.Value;
            }

            var zone = timezoneValidator.GetRequired(request.Timezone);

            if (request.ScheduledFor is not null)
            {
                var localScheduled = TimeZoneInfo.ConvertTime(request.ScheduledFor.Value, zone);
                return DateOnly.FromDateTime(localScheduled.DateTime);
            }

            var localNow = TimeZoneInfo.ConvertTime(clock.UtcNow, zone);
            return DateOnly.FromDateTime(localNow.DateTime);
        }
    }
}
