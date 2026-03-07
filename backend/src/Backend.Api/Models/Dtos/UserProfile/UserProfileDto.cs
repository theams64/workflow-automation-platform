namespace Backend.Api.Models.Dtos.User
{
    public sealed class UserProfileDto
    {
        public int IdentityUserId { get; set; }
        public string Email { get; set; } = default!;
        public string? DisplayName { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }
}
