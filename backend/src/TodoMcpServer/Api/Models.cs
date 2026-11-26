namespace TodoMcpServer.Api;

// Wire models mirroring the Todo API's JSON (camelCase, enums as strings, dates as yyyy-MM-dd).
// Only the fields this adapter needs are modelled; unknown fields are ignored.

public enum TodoPriority { Low, Medium, High }

public enum TodoStatus { Pending, InProgress, Completed }

public enum TodoDueFilter { Today, Upcoming, Overdue, None }

public sealed record SectionInfo(
    int Id,
    string Name,
    string Color,
    string Icon,
    int SortOrder,
    bool IsDefault,
    int TotalCount,
    int PendingCount,
    int InProgressCount,
    int CompletedCount);

public sealed record TagInfo(int Id, string Name, string Color);

public sealed record SubTaskInfo(int Id, int TaskId, string Title, bool IsCompleted, int SortOrder);

public sealed record TaskInfo(
    int Id,
    int SectionId,
    string Title,
    string? Description,
    DateOnly? DueDate,
    TodoPriority Priority,
    TodoStatus Status,
    DateTime? CompletedAt,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<TagInfo> Tags,
    IReadOnlyList<SubTaskInfo> SubTasks);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount, int TotalPages);

public sealed record AuthResponse(string AccessToken, DateTime ExpiresAt);

/// <summary>Error body returned by the API for every non-2xx response.</summary>
public sealed record ApiErrorBody(int Status, string Code, string Message, Dictionary<string, string[]>? Errors, string? TraceId);

public sealed record SubTaskPayload(string Title, bool IsCompleted = false, int? Id = null);

public sealed record CreateTaskPayload(
    int SectionId,
    string Title,
    string? Description,
    DateOnly? DueDate,
    TodoPriority Priority,
    TodoStatus Status,
    IReadOnlyList<int> TagIds,
    IReadOnlyList<SubTaskPayload> SubTasks);

/// <summary>PUT /api/tasks/{id} replaces the task, so every field is sent. SubTasks stays null (= leave the checklist alone).</summary>
public sealed record UpdateTaskPayload(
    int SectionId,
    string Title,
    string? Description,
    DateOnly? DueDate,
    TodoPriority Priority,
    TodoStatus Status,
    IReadOnlyList<int> TagIds);

public sealed record TaskListQuery(
    int? SectionId = null,
    TodoStatus? Status = null,
    TodoDueFilter? Due = null,
    int? TagId = null,
    string? Search = null,
    int PageSize = 50,
    DateOnly? Today = null);
