namespace Backend.Api.Models.Dtos.ManagedConnection
{
    public sealed class ManagedConnectionListResponseDto
    {
        public List<ManagedConnectionResponseDto> Items { get; set; } = [];
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }

        public int TotalPages => TotalCount == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
    }
}