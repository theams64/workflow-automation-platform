using Cronos;

namespace Backend.Api.Services.Common
{
    public sealed class CronExpressionValidator : ICronExpressionValidator
    {
        public bool IsValid(string cronExpression)
        {
            if (string.IsNullOrWhiteSpace(cronExpression))
            {
                return false;
            }

            try
            {
                CronExpression.Parse(cronExpression, CronFormat.Standard);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
