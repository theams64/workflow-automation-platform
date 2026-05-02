namespace Backend.Api.Services.Common
{
    public interface ICronExpressionValidator
    {
        bool IsValid(string cronExpression);
    }
}
