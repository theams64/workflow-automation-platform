using Backend.Api.Data;
using Backend.Api.Models.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
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

        public static AppDbContext CreateUnusedDbContext()
        {
            var options = new DbContextOptionsBuilder<AppDbContext>().Options;
            return new AppDbContext(options);
        }
    }
}
