using Backend.Api.Services.Common;
using FluentAssertions;

namespace Backend.Api.Tests.Services
{
    public sealed class JsonValidationHelperTests
    {
        private readonly JsonValidationHelper _sut = new();

        [Theory]
        [InlineData("{\"key\":\"value\"}")]
        [InlineData("[1,2,3]")]
        [InlineData("true")]
        [InlineData("123")]
        [InlineData("\"text\"")]
        public void IsValidJson_ShouldReturnTrue_WhenJsonIdValid(string json)
        {
            var result = _sut.IsValidJson(json);

            result.Should().BeTrue();
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("{")]
        [InlineData("not-json")]
        [InlineData("{\"key\":}")]
        public void IsValidJson_ShouldReturnFalse_WhenJsonIsinvalid(string json)
        {
            var result = _sut.IsValidJson(json);

            result.Should().BeFalse();
        }

        [Fact]
        public void IsValidJson_ShouldReturnFalse_WhenJsonIsNull()
        {
            var result = _sut.IsValidJson(null!);

            result.Should().BeFalse();
        }
    }
}
