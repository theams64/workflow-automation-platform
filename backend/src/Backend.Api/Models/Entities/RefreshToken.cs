using System.ComponentModel.DataAnnotations.Schema;

namespace Backend.Api.Models.Entities
{
    [Table("refresh_token")]
    public sealed class RefreshToken
    {
        [Column("id")]
        public Guid Id { get; set; }

        [Column("user_id")]
        public int UserId { get; set; }

        public ApplicationUser User { get; set; } = null!;

        [Column("token_hash")]
        public string TokenHash { get; set; } = string.Empty;

        [Column("family_id")]
        public Guid FamilyId { get; set; }

        [Column("created_at")]
        public DateTimeOffset CreatedAt { get; set; }

        [Column("expires_at")]
        public DateTimeOffset ExpiresAt { get; set; }

        [Column("revoked_at")]
        public DateTimeOffset? RevokedAt { get; set; }

        [Column("replaced_by_token_id")]
        public Guid? ReplacedByTokenId { get; set; }

        [Column("revocation_reason")]
        public string? RevocationReason { get; set; }

        [NotMapped]
        public bool IsExpired => ExpiresAt <= DateTimeOffset.UtcNow;

        [NotMapped]
        public bool IsActive => RevokedAt is null && !IsExpired;
    }
}
