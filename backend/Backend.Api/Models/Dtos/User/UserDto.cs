namespace Backend.Api.Models.Dtos.User
{
    public sealed class UserDto
    {
        public int Id { get; set; }
        public string Email { get; set; } = default!;
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }
}
