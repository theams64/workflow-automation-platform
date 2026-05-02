using System.Security.Claims;

namespace Backend.Api.Services.Common
{
    public sealed class CurrentUserService : ICurrentUserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CurrentUserService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public int GetUserId()
        {
            var user = _httpContextAccessor.HttpContext?.User;
            var userIdValue = user?.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrWhiteSpace(userIdValue))
            {
                throw new UnauthorizedAccessException("Authenticated user ID was not found");
            }

            if (!int.TryParse(userIdValue, out var userId))
            {
                throw new UnauthorizedAccessException("Authenticated user ID is invalid");
            }

            return userId;
        }
    }
}
