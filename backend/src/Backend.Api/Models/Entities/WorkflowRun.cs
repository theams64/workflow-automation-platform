using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend.Api.Models.Entities;

[Table("workflow_run")]
public class WorkflowRun
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int ID { get; set; }

    [Column("workflow_id")]
    public required int WorkflowID { get; set; }

    [ForeignKey(nameof(WorkflowID))]
    public Workflow? Workflow { get; set; }

    [Column("status")]
    public string? Status { get; set; }

    [Column("triggered_by")]
    public string? TriggeredBy { get; set; }

    [Column("started_at")]
    public required DateTimeOffset StartedAt { get; set; }

    [Column("finished_at")]
    public DateTimeOffset? FinishedAt { get; set; }

    [Column("input_payload")]
    public string? InputPayload { get; set; }
}
