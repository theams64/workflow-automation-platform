namespace Backend.Api.Services.Common
{
    public sealed record ServiceError(string Code, string Message);
    
    public sealed class ServiceResult<T>
    {
        public bool Succeeded { get; init; }
        public T? Data { get; init; }
        public IReadOnlyList<ServiceError> Errors { get; init; } = Array.Empty<ServiceError>();

        public static ServiceResult<T> Ok(T data) => new() { Succeeded = true, Data = data };

        public static ServiceResult<T> Fail(params ServiceError[] errors) =>
            new() { Succeeded = false, Errors = errors };

        public static ServiceResult<T> Fail(IEnumerable<ServiceError> errors) =>
            new() { Succeeded = false, Errors = errors.ToArray() };
    }
}
