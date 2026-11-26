namespace TodoApp.Api.Domain;

/// <summary>Task priority. Stored as an integer so it can be sorted numerically.</summary>
public enum TodoPriority
{
    Low = 0,
    Medium = 1,
    High = 2
}

/// <summary>Task status. Stored as an integer; serialized as a string in the API.</summary>
public enum TodoStatus
{
    Pending = 0,
    InProgress = 1,
    Completed = 2
}

/// <summary>Due-date buckets used by the task list filter.</summary>
public enum DueFilter
{
    /// <summary>Due exactly today.</summary>
    Today,
    /// <summary>Due after today.</summary>
    Upcoming,
    /// <summary>Due before today and not completed.</summary>
    Overdue,
    /// <summary>No due date set.</summary>
    None
}

public enum TaskSortBy
{
    /// <summary>User-defined (drag and drop) order. Default.</summary>
    Manual,
    DueDate,
    Priority,
    Created,
    Title
}

public enum SortDirection
{
    Asc,
    Desc
}

/// <summary>Names of the themes the backend accepts as a user preference.</summary>
public static class ThemeNames
{
    public const string Default = "light";

    public static readonly IReadOnlyList<string> All = new[] { "light", "dark", "ocean", "forest", "sunset" };

    public static bool IsValid(string? theme) => theme is not null && All.Contains(theme);
}
