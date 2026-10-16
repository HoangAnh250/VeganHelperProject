namespace VeganHelper.BLL.DTOs;

public sealed record ServiceResult<T>(bool Succeeded, T? Data, string? Error, int StatusCode, string? ErrorCode = null)
{
    public static ServiceResult<T> Ok(T data) => new(true, data, null, 200);
    public static ServiceResult<T> Created(T data) => new(true, data, null, 201);
    public static ServiceResult<T> Fail(string error, int statusCode, string? errorCode = null) =>
        new(false, default, error, statusCode, errorCode);
}
