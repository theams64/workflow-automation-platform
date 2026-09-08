using Backend.Api.Data;
using Backend.Api.Models.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Backend.Api.Tests.Common
{
    public static class TestHelpers
    {
        public static Mock<UserManager<ApplicationUser>> CreateMockUserManager()
        {
            var store = new Mock<IUserStore<ApplicationUser>>();

            return new Mock<UserManager<ApplicationUser>>(
                store.Object,
                null!,
                null!,
                null!,
                null!,
                null!,
                null!,
                null!,
                null!);
        }

        public static Mock<SignInManager<ApplicationUser>> CreateMockSignInManager(UserManager<ApplicationUser> userManager)
        {
            var contextAccessor = new Mock<IHttpContextAccessor>();
            var claimsFactory = new Mock<IUserClaimsPrincipalFactory<ApplicationUser>>();
            var authenticationSchemeProvider = new Mock<IAuthenticationSchemeProvider>();
            var userConfirmation = new Mock<IUserConfirmation<ApplicationUser>>();

            return new Mock<SignInManager<ApplicationUser>>(
                userManager,
                contextAccessor.Object,
                claimsFactory.Object,
                Options.Create(new IdentityOptions()),
                Mock.Of<ILogger<SignInManager<ApplicationUser>>>(),
                authenticationSchemeProvider.Object,
                userConfirmation.Object);
        }

        public static AppDbContext CreateUnusedDbContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().Options;
            return new AppDbContext(options);
        }

        public static AppDbContext CreateInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            return new AppDbContext(options);
        }
    }
}
