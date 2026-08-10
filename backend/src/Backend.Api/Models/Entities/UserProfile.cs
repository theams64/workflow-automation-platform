using Backend.Api.Models.Entities.Common;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend.Api.Models.Entities
{
    [Table("user_profile")]
    public sealed class UserProfile : IAuditable
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        [Column("id")]
        public int Id { get; set; }

        [Column("identity_user_id")]
        public int IdentityUserId { get; set; }

        [ForeignKey(nameof(IdentityUserId))]
        public ApplicationUser IdentityUser { get; set; } = default!;

        [Column("display_name")]
        public string? DisplayName { get; set; }

        [Column("created_at")]
        public DateTimeOffset CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTimeOffset UpdatedAt { get; set; }
    }
}
