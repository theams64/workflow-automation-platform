using Backend.Api.Services.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using System.Security.Claims;

namespace Backend.Api.Tests.Services
{
    public sealed class CurrentUserServiceTests
    {
        [Fact]
        public void GetUserId_ShouldReturnUserId_WhenNameIdentifierClaimExists()
        {
            var httpContextAccessor = new HttpContextAccessor
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "42")], "TestAuth"))
                }
            };

            var sut = new CurrentUserService(httpContextAccessor);

            var result = sut.GetUserId();

            result.Should().Be(42);
        }

        [Fact]
        public void GetUserId_ShouldThrowUnauthorizedAccessException_WhenClaimIsMissing()
        {
            var httpContextAccessor = new HttpContextAccessor
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity())
                }
            };

            var sut = new CurrentUserService(httpContextAccessor);

            Action act = () => sut.GetUserId();

            act.Should().Throw<UnauthorizedAccessException>()
                .WithMessage("*Authenticated user ID was not found*");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("not-an-integer")]
        [InlineData("0")]
        [InlineData("-1")]
        public void GetUserId_ShouldThrow_WhenClaimIsInvalid(string? claimValue)
        {
            var claims = new List<Claim>();

            if (claimValue is not null)
            {
                claims.Add(new Claim(ClaimTypes.NameIdentifier, claimValue));
            }

            var context = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "Test"))
            };

            var accessor = new Mock<IHttpContextAccessor>();

            accessor
                .SetupGet(service => service.HttpContext)
                .Returns(context);

            var sut = new CurrentUserService(accessor.Object);

            var action = () => sut.GetUserId();

            action.Should().Throw<UnauthorizedAccessException>();
        }

        [Fact]
        public void GetUserId_ShouldReturnPositiveUserId()
        {
            var context = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, "42")
                ],
                authenticationType: "Test"))
            };

            var accessor = new Mock<IHttpContextAccessor>();

            accessor
                .SetupGet(service => service.HttpContext)
                .Returns(context);

            var sut = new CurrentUserService(accessor.Object);

            sut.GetUserId().Should().Be(42);
        }
    }
}
