using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Backend.Api.Models.Entities;

[Table("step_run")]
public class StepRun
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public int ID { get; set; }

    [Column("workflow_run_id")]
    public required int WorkflowRunID { get; set; }

    [ForeignKey(nameof(WorkflowRunID))]
    public required WorkflowRun WorkflowRun { get; set; }

    [Column("workflow_step_id")]
    public required int WorkflowStepID { get; set; }

    [ForeignKey(nameof(WorkflowStepID))]
    public required WorkflowRun WorkflowStep { get; set; }

    [Column("status")]
    public string? Status { get; set; }

    [Column("started_at")]
    public required DateTimeOffset StartedAt { get; set; }

    [Column("finished_at")]
    public DateTimeOffset? FinishedAt { get; set; }

    [Column("input_json")]
    public required string InputJson { get; set; }

    [Column("output_json")]
    public string? OutputJson { get; set; }

    [Column("error")]
    public string? Error { get; set; }

    [Column("attempt")]
    public required int Attempt { get; set; }
}
