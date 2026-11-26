using System.ComponentModel.DataAnnotations;
using TodoApp.Api.Domain;

namespace TodoApp.Api.Dtos;

public record TagDto(int Id, string Name, string Color);

public record TagListItemDto(int Id, string Name, string Color, int TaskCount);

public record SubTaskDto(int Id, int TaskId, string Title, bool IsCompleted, int SortOrder);

public record TaskDto(
    int Id,
    int SectionId,
    string Title,
    string? Description,
    DateOnly? DueDate,
    TodoPriority Priority,
    TodoStatus Status,
    int SortOrder,
    DateTime? CompletedAt,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<TagDto> Tags,
    IReadOnlyList<SubTaskDto> SubTasks);

/// <summary>Query-string options for GET /api/tasks.</summary>
public class TaskQuery
{
    /// <summary>Case-insensitive text search over title and description.</summary>
    [MaxLength(200)]
    public string? Search { get; set; }
    public TodoStatus? Status { get; set; }
    public TodoPriority? Priority { get; set; }
    public DueFilter? Due { get; set; }
    public int? TagId { get; set; }
    public int? SectionId { get; set; }
    public TaskSortBy? SortBy { get; set; }
    /// <summary>Defaults depend on <c>sortBy</c>: due date asc, priority desc (High first), created desc, title asc.</summary>
    public SortDirection? SortDir { get; set; }

    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 200)]
    public int PageSize { get; set; } = 50;

    /// <summary>The caller's local date, used for Today/Upcoming/Overdue. Defaults to today in UTC.</summary>
    public DateOnly? Today { get; set; }
}

public class SubTaskInput
{
    /// <summary>Existing sub-task id; omit to create a new one.</summary>
    public int? Id { get; set; }

    [Required, StringLength(200, MinimumLength = 1)]
    public string Title { get; set; } = string.Empty;

    public bool IsCompleted { get; set; }
}

public class CreateTaskRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "A section is required.")]
    public int SectionId { get; set; }

    [Required, StringLength(200, MinimumLength = 1)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(4000)]
    public string? Description { get; set; }

    public DateOnly? DueDate { get; set; }
    public TodoPriority Priority { get; set; } = TodoPriority.Medium;
    public TodoStatus Status { get; set; } = TodoStatus.Pending;

    [MaxLength(20)]
    public List<int> TagIds { get; set; } = new();

    [MaxLength(100)]
    public List<SubTaskInput> SubTasks { get; set; } = new();
}

public class UpdateTaskRequest
{
    /// <summary>Changing the section moves the task to the end of the target section.</summary>
    [Range(1, int.MaxValue, ErrorMessage = "A section is required.")]
    public int SectionId { get; set; }

    [Required, StringLength(200, MinimumLength = 1)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(4000)]
    public string? Description { get; set; }

    public DateOnly? DueDate { get; set; }
    public TodoPriority Priority { get; set; } = TodoPriority.Medium;
    public TodoStatus Status { get; set; } = TodoStatus.Pending;

    [MaxLength(20)]
    public List<int> TagIds { get; set; } = new();

    /// <summary>
    /// Optional. When provided the task's checklist is synchronised to this list
    /// (ids update, no id creates, missing ids are deleted). When omitted the checklist is untouched.
    /// </summary>
    [MaxLength(100)]
    public List<SubTaskInput>? SubTasks { get; set; }
}

public class CompletionRequest
{
    public bool IsCompleted { get; set; }
}

public class MoveTaskRequest
{
    [Range(1, int.MaxValue, ErrorMessage = "A section is required.")]
    public int SectionId { get; set; }
}

public class BulkIdsRequest
{
    [Required, MinLength(1), MaxLength(500)]
    public List<int> Ids { get; set; } = new();
}

public class BulkCompleteRequest : BulkIdsRequest
{
    public bool IsCompleted { get; set; } = true;
}

public class CreateSubTaskRequest
{
    [Required, StringLength(200, MinimumLength = 1)]
    public string Title { get; set; } = string.Empty;
}

public class UpdateSubTaskRequest
{
    [Required, StringLength(200, MinimumLength = 1)]
    public string Title { get; set; } = string.Empty;

    public bool IsCompleted { get; set; }
}

public class TagRequest
{
    [Required, StringLength(50, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [Required, RegularExpression("^#[0-9a-fA-F]{6}$", ErrorMessage = "Color must be a hex value like #3b82f6.")]
    public string Color { get; set; } = "#64748b";
}
