namespace NguyenBinh.Shared.Results;

/// <summary>
/// Envelope chuan cho moi response cua API: { success, data, message, errors }.
/// </summary>
public sealed class ApiResponse<T>
{
    public bool Success { get; init; }
    public T? Data { get; init; }
    public string? Message { get; init; }

    /// <summary>Loi theo field (validation) hoac theo ma loi. Null khi thanh cong.</summary>
    public IReadOnlyDictionary<string, string[]>? Errors { get; init; }
}

public static class ApiResponse
{
    public static ApiResponse<T> Ok<T>(T data, string? message = null) =>
        new() { Success = true, Data = data, Message = message };

    public static ApiResponse<object?> Fail(string message, IReadOnlyDictionary<string, string[]>? errors = null) =>
        new() { Success = false, Message = message, Errors = errors };
}
