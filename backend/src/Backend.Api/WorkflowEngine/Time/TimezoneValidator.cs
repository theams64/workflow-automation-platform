namespace Backend.Api.WorkflowEngine.Time
{
    public sealed class TimezoneValidator : ITimezoneValidator
    {
        public bool IsValid(string timezone)
        {
            if (string.IsNullOrWhiteSpace(timezone))
            {
                return false;
            }

            try
            {
                _ = TimeZoneInfo.FindSystemTimeZoneById(timezone);
                return true;
            }
            catch (TimeZoneNotFoundException)
            {
                return false;
            }
            catch (InvalidTimeZoneException)
            {
                return false;
            }
        }

        public TimeZoneInfo GetRequired(string timezone)
        {
            if (!IsValid(timezone))
            {
                throw new ArgumentException("The workflow timezone is invalid.", nameof(timezone));
            }

            return TimeZoneInfo.FindSystemTimeZoneById(timezone);
        }
    }
}
