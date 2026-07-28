using Backend.Api.Data;
using Backend.Api.Models.Entities;
using Backend.Api.Services.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Moq;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;

namespace Backend.Api.Tests.Common
{
    public static class WorkflowTestHelpers
    {
        public static AppDbContext CreateInMemoryDbContext(string? databaseName = null)
        {
            databaseName ??= Guid.NewGuid().ToString();

            var options = new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(databaseName)
                .Options;

            return new AppDbContext(options);
        }

        public static Mock<ICurrentUserService> CreateCurrentUserServiceMock(int userId = 1)
        {
            var mock = new Mock<ICurrentUserService>();
            mock.Setup(x => x.GetUserId()).Returns(userId);
            return mock;
        }

        public static Mock<ICronExpressionValidator> CreateCronValidatorMock(bool isValid = true)
        {
            var mock = new Mock<ICronExpressionValidator>();
            mock.Setup(x => x.IsValid(It.IsAny<string>())).Returns(isValid);
            return mock;
        }

        public static Mock<IJsonValidationHelper> CreateJsonValidatorMock(bool isValid = true)
        {
            var mock = new Mock<IJsonValidationHelper>();
            mock.Setup(x => x.IsValidJson(It.IsAny<string>())).Returns(isValid);
            return mock;
        }

        public static string CreateJwtToken(int userId, string issuer = "https://localhost", string audience = "TestAudience", string key = "ThisIsATestJwtKeyThatIsLongEnough123!", int accessTokenMinutes = 10)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString())
            };

            var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
            var creds = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

            var now = DateTime.UtcNow;

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                notBefore: now.AddMinutes(-1),
                expires: now.AddMinutes(accessTokenMinutes),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
