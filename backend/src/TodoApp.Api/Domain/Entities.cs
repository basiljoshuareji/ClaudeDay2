namespace TodoApp.Api.Domain;

public class User
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public UserPreference? Preference { get; set; }
    public List<Section> Sections { get; set; } = new();
    public List<Tag> Tags { get; set; } = new();
    public List<TodoTask> Tasks { get; set; } = new();
}

public class UserPreference
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public string Theme { get; set; } = ThemeNames.Default;
    public bool SidebarCollapsed { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>A named list of tasks (e.g. Home, Work).</summary>
public class Section
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    /// <summary>Hex colour such as <c>#3b82f6</c>.</summary>
    public string Color { get; set; } = "#3b82f6";
    /// <summary>Icon key understood by the front end (e.g. "home", "briefcase").</summary>
    public string Icon { get; set; } = "folder";
    public int SortOrder { get; set; }
    /// <summary>Default sections (Home, Work) can be edited but not deleted.</summary>
    public bool IsDefault { get; set; }
    public DateTime CreatedAt { get; set; }

    public List<TodoTask> Tasks { get; set; } = new();
}

public class TodoTask
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public int SectionId { get; set; }
    public Section Section { get; set; } = null!;

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateOnly? DueDate { get; set; }
    public TodoPriority Priority { get; set; } = TodoPriority.Medium;
    public TodoStatus Status { get; set; } = TodoStatus.Pending;

    /// <summary>Position within the section for manual (drag and drop) ordering.</summary>
    public int SortOrder { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>Soft-delete marker; enables "Undo" after a delete. Purged by a background service.</summary>
    public DateTime? DeletedAt { get; set; }

    public List<SubTask> SubTasks { get; set; } = new();
    public List<Tag> Tags { get; set; } = new();
}

public class SubTask
{
    public int Id { get; set; }
    public int TodoTaskId { get; set; }
    public TodoTask TodoTask { get; set; } = null!;

    public string Title { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public int SortOrder { get; set; }
}

public class Tag
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#64748b";

    public List<TodoTask> Tasks { get; set; } = new();
}
