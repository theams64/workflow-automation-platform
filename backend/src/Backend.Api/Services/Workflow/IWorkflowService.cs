using Backend.Api.Models.Dtos.Workflow;
using Backend.Api.Models.Dtos.WorkflowStep;
using Backend.Api.Services.Common;

namespace Backend.Api.Services.Workflow
{
    public interface IWorkflowService
    {
        Task<ServiceResult<WorkflowResponseDto>> CreateWorkflowAsync(CreateWorkflowRequestDto request, CancellationToken cancellationToken = default);
        Task<ServiceResult<WorkflowListResponseDto>> GetWorkflowsAsync(WorkflowListRequestDto request, CancellationToken cancellationToken = default);
        Task<ServiceResult<WorkflowDetailResponseDto>> GetWorkflowByIdAsync(int workflowId, CancellationToken cancellationToken = default);
        Task<ServiceResult<WorkflowResponseDto>> UpdateWorkflowAsync(int workflowId, UpdateWorkflowRequestDto request, CancellationToken cancellationToken = default);
        Task<ServiceResult<bool>> DeleteWorkflowAsync(int workflowId, CancellationToken cancellationToken = default);
        Task<ServiceResult<WorkflowStepListResponseDto>> GetWorkflowStepsAsync(int workflowId, CancellationToken cancellationToken = default);
        Task<ServiceResult<WorkflowStepListResponseDto>> CreateWorkflowStepsAsync(int workflowId, SaveWorkflowStepsRequestDto request, CancellationToken cancellationToken = default);
        Task<ServiceResult<WorkflowStepListResponseDto>> ReplaceWorkflowStepsAsync(int workflowId, SaveWorkflowStepsRequestDto request, CancellationToken cancellationToken = default);
        Task<ServiceResult<bool>> DeleteWorkflowStepsAsync(int workflowId, CancellationToken cancellationToken = default);
    }
}
