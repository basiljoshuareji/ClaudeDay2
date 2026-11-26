using TodoApp.Api.Domain;
using TodoApp.Api.Dtos;

namespace TodoApp.Api.Mapping;

/// <summary>Manual entity → DTO mapping (explicit, fast and easy to debug; no AutoMapper dependency).</summary>
public static class Mappings
{
    public static UserDto ToDto(this User u) => new(u.Id, u.Email, u.DisplayName);

    public static PreferenceDto ToDto(this UserPreference p) => new(p.Theme, p.SidebarCollapsed);

    public static TagDto ToDto(this Tag t) => new(t.Id, t.Name, t.Color);

    public static SubTaskDto ToDto(this SubTask s) => new(s.Id, s.TodoTaskId, s.Title, s.IsCompleted, s.SortOrder);

    public static SectionDto ToDto(this Section s, int total = 0, int pending = 0, int inProgress = 0, int completed = 0) =>
        new(s.Id, s.Name, s.Color, s.Icon, s.SortOrder, s.IsDefault, total, pending, inProgress, completed);

    public static TaskDto ToDto(this TodoTask t) => new(
        t.Id,
        t.SectionId,
        t.Title,
        t.Description,
        t.DueDate,
        t.Priority,
        t.Status,
        t.SortOrder,
        t.CompletedAt,
        t.CreatedAt,
        t.UpdatedAt,
        t.Tags.OrderBy(x => x.Name).Select(x => x.ToDto()).ToList(),
        t.SubTasks.OrderBy(x => x.SortOrder).ThenBy(x => x.Id).Select(x => x.ToDto()).ToList());
}
