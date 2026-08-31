using Backend.Api.Models.Entities.Common;
using Backend.Api.Models.Validation;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend.Api.Models.Entities
{
    [Table("managed_connection")]
    public sealed class ManagedConnection : IAuditable
    {
        [Key]
        [Column("id")]
        public Guid ID { get; set; }

        [Column("user_id")]
        public required int UserID { get; set; }

        public ApplicationUser? User { get; set; }

        [Column("name")]
        [MaxLength(WorkflowLimits.ConnectionNameMaxLength)]
        public required string Name { get; set; }

        [Column("connection_type")]
        [MaxLength(WorkflowLimits.ConnectionTypeMaxLength)]
        public required string ConnectionType { get; set; }

        [Column("canonical_origin")]
        [MaxLength(WorkflowLimits.ConnectionCanonicalOriginMaxLength)]
        public required string CanonicalOrigin { get; set; }

        [Column("credential_type")]
        [MaxLength(WorkflowLimits.ConnectionCredentialTypeMaxLength)]
        public required string CredentialType { get; set; }

        [Column("secret_reference")]
        [MaxLength(WorkflowLimits.ConnectionSecretReferenceMaxLength)]
        public required string SecretReference { get; set; }

        [Column("credential_placement")]
        [MaxLength(WorkflowLimits.ConnectionCredentialPlacementMaxLength)]
        public required string CredentialPlacement { get; set; }

        [Column("is_enabled")]
        public required bool IsEnabled { get; set; }

        [Column("revoked_at")]
        public DateTimeOffset? RevokedAt { get; set; }

        [Column("created_at")]
        public DateTimeOffset CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTimeOffset UpdatedAt { get; set; }
    }
}