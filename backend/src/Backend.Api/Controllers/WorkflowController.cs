using Backend.Api.Models.Dtos.Workflow;
using Backend.Api.Models.Dtos.WorkflowStep;
using Backend.Api.Services.Workflow;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Api.Controllers
{
    [ApiController]
    [Route("workflow")]
    public sealed class WorkflowController : ControllerBase
    {
        private readonly IWorkflowService _workflow;

        public WorkflowController(IWorkflowService workflow)
        {
            _workflow = workflow;
        }

        [HttpPost]
        public async Task<ActionResult<WorkflowResponseDto>> CreateWorkflow([FromBody] CreateWorkflowRequestDto dto, CancellationToken ct)
        {
            var result = await _workflow.CreateWorkflowAsync(dto, ct);

            if (!result.Succeeded)
            {
                var hasConflict = result.Errors.Any(e => e.Code == "workflow.duplicate_name");
                if (hasConflict)
                {
                    return base.Conflict(new { errors = result.Errors });
                }

                return base.BadRequest(new { errors = result.Errors });
            }

            return base.CreatedAtAction(nameof(GetWorkflowById), new { workflowId = result.Data!.Id }, result.Data);
        }

        [HttpGet]
        public async Task<ActionResult<List<WorkflowResponseDto>>> GetAllWorkflows(CancellationToken ct)
        {
            var result = await _workflow.GetWorkflowsAsync(ct);

            if (!result.Succeeded)
            {
                var hasUnauthorized = result.Errors.Any(e => e.Code == "auth.unauthorized");
                if (hasUnauthorized)
                {
                    return base.Unauthorized(new { errors = result.Errors });
                }

                return base.BadRequest(new { errors = result.Errors });
            }

            return base.Ok(result.Data);
        }

        [HttpGet("{workflowId:int}")]
        public async Task<ActionResult<WorkflowDetailResponseDto>> GetWorkflowById(int workflowId, CancellationToken ct)
        {
            var result = await _workflow.GetWorkflowByIdAsync(workflowId, ct);

            if (!result.Succeeded)
            {
                var hasUnauthorized = result.Errors.Any(e => e.Code == "auth.unauthorized");
                if (hasUnauthorized)
                {
                    return base.Unauthorized(new { errors = result.Errors });
                }

                var hasNotFound = result.Errors.Any(e => e.Code == "workflow.not_found");
                if (hasNotFound)
                {
                    return base.NotFound(new { errors = result.Errors });
                }

                return base.BadRequest(new { errors = result.Errors });
            }

            return base.Ok(result.Data);
        }

        [HttpPut("{workflowId:int}")]
        public async Task<ActionResult<WorkflowResponseDto>> UpdateWorkflow(int workflowId, [FromBody] UpdateWorkflowRequestDto dto, CancellationToken ct)
        {
            var result = await _workflow.UpdateWorkflowAsync(workflowId, dto, ct);

            if (!result.Succeeded)
            {
                var hasUnauthorized = result.Errors.Any(e => e.Code == "auth.unauthorized");
                if (hasUnauthorized)
                {
                    return base.Unauthorized(new { errors = result.Errors });
                }

                var hasNotFound = result.Errors.Any(e => e.Code == "workflow.not_found");
                if (hasNotFound)
                {
                    return base.NotFound(new { errors = result.Errors });
                }

                return base.BadRequest(new { errors = result.Errors });
            }

            return base.Ok(result.Data);
        }

        [HttpDelete("{workflowId:int}")]
        public async Task<ActionResult> DeleteWorkflow(int workflowId, CancellationToken ct)
        {
            var result = await _workflow.DeleteWorkflowAsync(workflowId, ct);

            if (!result.Succeeded)
            {
                var hasUnauthorized = result.Errors.Any(e => e.Code == "auth.unauthorized");
                if (hasUnauthorized)
                {
                    return base.Unauthorized(new { errors = result.Errors });
                }

                var hasNotFound = result.Errors.Any(e => e.Code == "workflow.not_found");
                if (hasNotFound)
                {
                    return base.NotFound(new { errors = result.Errors });
                }

                return base.BadRequest(new { errors = result.Errors });
            }

            return base.NoContent();
        }
    }
}
