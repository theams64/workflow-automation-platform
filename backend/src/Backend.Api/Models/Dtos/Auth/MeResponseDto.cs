namespace Backend.Api.Models.Dtos.Auth
{
    public sealed class MeResponseDto
    {
        public int Id { get; set; }
        public string Email { get; set; } = default!;
    }
}
