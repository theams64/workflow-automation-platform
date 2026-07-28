namespace Backend.Api.Models.Dtos.Workflow
{
    public sealed class WorkflowListResponseDto
    {
        public List<WorkflowResponseDto> Items { get; set; } = new();

        public int Page { get; set; }

        public int PageSize { get; set; }

        public int TotalCount { get; set; }

        public int TotalPages => TotalCount == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    }
}
