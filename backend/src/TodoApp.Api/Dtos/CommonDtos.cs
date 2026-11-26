namespace TodoApp.Api.Dtos;

/// <summary>Consistent error body returned for every non-2xx response.</summary>
/// <param name="Status">HTTP status code.</param>
/// <param name="Code">Stable machine-readable code (validation_failed, not_found, conflict, ...).</param>
/// <param name="Message">Human-readable summary.</param>
/// <param name="Errors">Per-field validation messages, when applicable.</param>
/// <param name="TraceId">Correlates the response with server logs.</param>
public record ApiError(
    int Status,
    string Code,
    string Message,
    IDictionary<string, string[]>? Errors,
    string? TraceId);

public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public record BulkResultDto(int Affected);
