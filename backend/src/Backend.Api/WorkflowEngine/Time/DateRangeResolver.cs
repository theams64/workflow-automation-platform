namespace Backend.Api.WorkflowEngine.Time
{
    public sealed class DateRangeResolver : IDateRangeResolver
    {
        public (DateOnly Start, DateOnly End) ResolveForwardRange(DateOnly effectiveDate, int daysInAdvance, int maximumDaysInAdvance)
        {
            if (daysInAdvance < 0 || daysInAdvance > maximumDaysInAdvance)
            {
                throw new ArgumentOutOfRangeException(nameof(daysInAdvance));
            }

            return (effectiveDate, effectiveDate.AddDays(daysInAdvance));
        }
    }
}
