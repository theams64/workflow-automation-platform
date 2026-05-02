using Backend.Api.Services.Common;
using FluentAssertions;

namespace Backend.Api.Tests.Services
{
    public sealed class CronExpressionValidatorTests
    {
        private readonly CronExpressionValidator _sut = new();

        [Theory]
        [InlineData("0 0 * * *")]
        [InlineData("*/5 * * * *")]
        [InlineData("15 10 * * 1")]
        public void IsValid_ShouldReturnTrue_WhenCronExpressionIsValid(string cronExpression)
        {
            var result = _sut.IsValid(cronExpression);

            result.Should().BeTrue();
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("* * *")]
        [InlineData("not-a-cron")]
        [InlineData("60 99 * * *")]
        public void IsValid_ShouldReturnFalse_WhenCronExpressionIsInvalid(string cronExpression)
        {
            var result = _sut.IsValid(cronExpression);

            result.Should().BeFalse();
        }

        [Fact]
        public void IsValid_ShouldReturnFalse_WhenCronExpressionIsNull()
        {
            var result = _sut.IsValid(null!);

            result.Should().BeFalse();
        }
    }
}
