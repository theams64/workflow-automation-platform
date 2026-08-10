namespace Backend.Api.WorkflowEngine.Time
{
    public interface IDateRangeResolver
    {
        (DateOnly Start, DateOnly End) ResolveForwardRange(DateOnly effectiveDate, int daysInAdvance, int maximumDaysInAdvance);
    }
}
