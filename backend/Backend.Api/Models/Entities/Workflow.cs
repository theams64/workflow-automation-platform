using Backend.Api.Models.Entities.Common;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend.Api.Models.Entities;

[Table("workflow")]
public class Workflow : IAuditable
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int ID { get; set; }

    [Column("user_id")]
    public required int UserID { get; set; }

    [ForeignKey(nameof(UserID))]
    public required User User { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    [Column("is_enabled")]
    public required bool IsEnabled { get; set; }

    [Column("trigger_type")]
    public required string? TriggerType { get; set; }

    [Column("cron_expression")]
    public string? CronExpression { get; set; }

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; }
}
