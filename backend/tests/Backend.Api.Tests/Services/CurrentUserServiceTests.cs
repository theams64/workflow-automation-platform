using Backend.Api.Services.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
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

        [Fact]
        public void GetUserId_ShouldThrowUnauthorizedAccessException_WhenClaimIsInvalid()
        {
            var httpContextAccessor = new HttpContextAccessor
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "abc")], "TestAuth"))
                }
            };

            var sut = new CurrentUserService(httpContextAccessor);

            Action act = () => sut.GetUserId();

            act.Should().Throw<UnauthorizedAccessException>()
                .WithMessage("*Authenticated user ID is invalid");
        }
    }
}
