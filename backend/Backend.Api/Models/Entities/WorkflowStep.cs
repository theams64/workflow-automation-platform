using Backend.Api.Models.Entities.Common;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend.Api.Models.Entities;

[Table("workflow_step")]
public class WorkflowStep : IAuditable
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int ID { get; set; }

    [Column("workflow_id")]
    public required int WorkflowID { get; set; }

    [ForeignKey(nameof(WorkflowID))]
    public required Workflow Workflow {  get; set; }

    [Column("step_order")]
    public required int StepOrder {  get; set; }

    [Column("step_type")]
    public required string StepType { get; set; }

    [Column("config_json")]
    public required string ConfigJson { get; set; }

    [Column("created_at")]
    public DateTimeOffset CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTimeOffset UpdatedAt { get; set; }
}
