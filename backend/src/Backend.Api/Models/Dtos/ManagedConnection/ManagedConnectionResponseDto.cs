namespace Backend.Api.Models.Dtos.ManagedConnection
{
    public sealed class ManagedConnectionResponseDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = default!;
        public string ConnectionType { get; set; } = default!;
        public string CanonicalOrigin { get; set; } = default!;
        public bool IsEnabled { get; set; }
        public bool IsRevoked { get; set; }
        public DateTimeOffset? RevokedAt { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }
}