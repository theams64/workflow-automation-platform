namespace Backend.Api.Infrastructure.RateLimiting
{
    public static class RateLimitingPolicies
    {
        public const string Login = "login";
        public const string Registration = "registration";
        public const string PasswordReset = "password-reset";
        public const string TokenRefresh = "token-refresh";
    }
}
