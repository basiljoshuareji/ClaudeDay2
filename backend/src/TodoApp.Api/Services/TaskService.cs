using Microsoft.EntityFrameworkCore;
using TodoApp.Api.Data;
using TodoApp.Api.Domain;
using TodoApp.Api.Dtos;
using TodoApp.Api.Exceptions;
using TodoApp.Api.Mapping;

namespace TodoApp.Api.Services;

public class TaskService(AppDbContext db, TimeProvider clock, ILogger<TaskService> logger) : ITaskService
{
    // ----------------------------------------------------------------- queries

    public async Task<PagedResult<TaskDto>> GetTasksAsync(int userId, TaskQuery q, CancellationToken ct = default)
    {
        var today = q.Today ?? DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
        IQueryable<TodoTask> query = db.Tasks.AsNoTracking().Where(t => t.UserId == userId);

        if (q.SectionId is int sectionId)
        {
            query = query.Where(t => t.SectionId == sectionId);
        }

        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var pattern = $"%{EscapeLike(q.Search.Trim())}%";
            query = query.Where(t =>
                EF.Functions.Like(t.Title, pattern, "\\") ||
                (t.Description != null && EF.Functions.Like(t.Description, pattern, "\\")));
        }

        if (q.Status is TodoStatus status)
        {
            query = query.Where(t => t.Status == status);
        }

        if (q.Priority is TodoPriority priority)
        {
            query = query.Where(t => t.Priority == priority);
        }

        if (q.TagId is int tagId)
        {
            query = query.Where(t => t.Tags.Any(tag => tag.Id == tagId));
        }

        query = q.Due switch
        {
            DueFilter.Today => query.Where(t => t.DueDate == today),
            DueFilter.Upcoming => query.Where(t => t.DueDate > today),
            DueFilter.Overdue => query.Where(t => t.DueDate < today && t.Status != TodoStatus.Completed),
            DueFilter.None => query.Where(t => t.DueDate == null),
            _ => query
        };

        var total = await query.CountAsync(ct);
        var page = Math.Max(1, q.Page);
        var size = Math.Clamp(q.PageSize, 1, 200);

        var items = await ApplySort(query, q.SortBy ?? TaskSortBy.Manual, q.SortDir)
            .Skip((page - 1) * size)
            .Take(size)
            .Include(t => t.Tags)
            .Include(t => t.SubTasks)
            .AsSplitQuery()
            .ToListAsync(ct);

        return new PagedResult<TaskDto>(items.Select(t => t.ToDto()).ToList(), page, size, total);
    }

    public async Task<TaskDto> GetAsync(int userId, int id, CancellationToken ct = default) =>
        (await LoadAsync(userId, id, tracking: false, ct)).ToDto();

    // --------------------------------------------------------------- mutations

    public async Task<TaskDto> CreateAsync(int userId, CreateTaskRequest request, CancellationToken ct = default)
    {
        await EnsureSectionOwnedAsync(userId, request.SectionId, ct);
        var tags = await LoadTagsAsync(userId, request.TagIds, ct);
        var now = clock.GetUtcNow().UtcDateTime;

        var task = new TodoTask
        {
            UserId = userId,
            SectionId = request.SectionId,
            Title = request.Title.Trim(),
            Description = NullIfBlank(request.Description),
            DueDate = request.DueDate,
            Priority = request.Priority,
            Status = request.Status,
            CompletedAt = request.Status == TodoStatus.Completed ? now : null,
            SortOrder = await NextSortOrderAsync(userId, request.SectionId, ct),
            CreatedAt = now,
            UpdatedAt = now,
            Tags = tags
        };

        var order = 0;
        foreach (var sub in request.SubTasks)
        {
            task.SubTasks.Add(new SubTask { Title = sub.Title.Trim(), IsCompleted = sub.IsCompleted, SortOrder = order++ });
        }

        db.Tasks.Add(task);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("User {UserId} created task {TaskId}", userId, task.Id);
        return task.ToDto();
    }

    public async Task<TaskDto> UpdateAsync(int userId, int id, UpdateTaskRequest request, CancellationToken ct = default)
    {
        var task = await LoadAsync(userId, id, tracking: true, ct);
        var tags = await LoadTagsAsync(userId, request.TagIds, ct);
        var now = clock.GetUtcNow().UtcDateTime;

        if (task.SectionId != request.SectionId)
        {
            await EnsureSectionOwnedAsync(userId, request.SectionId, ct);
            task.SectionId = request.SectionId;
            task.SortOrder = await NextSortOrderAsync(userId, request.SectionId, ct);
        }

        task.Title = request.Title.Trim();
        task.Description = NullIfBlank(request.Description);
        task.DueDate = request.DueDate;
        task.Priority = request.Priority;
        ApplyStatus(task, request.Status, now);

        task.Tags.RemoveAll(existing => tags.All(t => t.Id != existing.Id));
        foreach (var tag in tags.Where(t => task.Tags.All(existing => existing.Id != t.Id)))
        {
            task.Tags.Add(tag);
        }

        if (request.SubTasks is not null)
        {
            SyncSubTasks(task, request.SubTasks);
        }

        task.UpdatedAt = now;
        await db.SaveChangesAsync(ct);
        return task.ToDto();
    }

    public async Task<TaskDto> SetCompletionAsync(int userId, int id, bool isCompleted, CancellationToken ct = default)
    {
        var task = await LoadAsync(userId, id, tracking: true, ct);
        var now = clock.GetUtcNow().UtcDateTime;

        if (isCompleted)
        {
            ApplyStatus(task, TodoStatus.Completed, now);
        }
        else if (task.Status == TodoStatus.Completed)
        {
            ApplyStatus(task, TodoStatus.Pending, now);
        }

        task.UpdatedAt = now;
        await db.SaveChangesAsync(ct);
        return task.ToDto();
    }

    public async Task<TaskDto> MoveAsync(int userId, int id, int sectionId, CancellationToken ct = default)
    {
        var task = await LoadAsync(userId, id, tracking: true, ct);
        if (task.SectionId != sectionId)
        {
            await EnsureSectionOwnedAsync(userId, sectionId, ct);
            task.SectionId = sectionId;
            task.SortOrder = await NextSortOrderAsync(userId, sectionId, ct);
            task.UpdatedAt = clock.GetUtcNow().UtcDateTime;
            await db.SaveChangesAsync(ct);
        }

        return task.ToDto();
    }

    /// <summary>
    /// Applies a new relative order to the given tasks. Other tasks in the section keep their slots,
    /// so this also works for a filtered (partial) list.
    /// </summary>
    public async Task ReorderAsync(int userId, IReadOnlyList<int> orderedIds, CancellationToken ct = default)
    {
        var ids = orderedIds.Distinct().ToList();
        var sectionIds = await db.Tasks
            .Where(t => t.UserId == userId && ids.Contains(t.Id))
            .Select(t => t.SectionId)
            .Distinct()
            .ToListAsync(ct);

        if (sectionIds.Count == 0 || await db.Tasks.CountAsync(t => t.UserId == userId && ids.Contains(t.Id), ct) != ids.Count)
        {
            throw new BadRequestException("One or more tasks do not exist.", "orderedIds");
        }

        if (sectionIds.Count > 1)
        {
            throw new BadRequestException("Tasks can only be reordered within a single section.", "orderedIds");
        }

        var all = await db.Tasks
            .Where(t => t.UserId == userId && t.SectionId == sectionIds[0])
            .OrderBy(t => t.SortOrder).ThenBy(t => t.Id)
            .ToListAsync(ct);

        var byId = all.Where(t => ids.Contains(t.Id)).ToDictionary(t => t.Id);
        var slots = all.Select((t, index) => (t.Id, index)).Where(x => byId.ContainsKey(x.Id)).Select(x => x.index).ToList();

        var arranged = all.ToList();
        for (var i = 0; i < ids.Count; i++)
        {
            arranged[slots[i]] = byId[ids[i]];
        }

        for (var i = 0; i < arranged.Count; i++)
        {
            arranged[i].SortOrder = i;
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int userId, int id, CancellationToken ct = default)
    {
        var task = await db.Tasks.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId, ct)
                   ?? throw new NotFoundException("Task");
        var now = clock.GetUtcNow().UtcDateTime;
        task.DeletedAt = now;
        task.UpdatedAt = now;
        await db.SaveChangesAsync(ct);
        logger.LogInformation("User {UserId} deleted task {TaskId}", userId, id);
    }

    public async Task<IReadOnlyList<TaskDto>> RestoreAsync(int userId, IReadOnlyList<int> ids, CancellationToken ct = default)
    {
        var tasks = await db.Tasks.IgnoreQueryFilters()
            .Where(t => t.UserId == userId && ids.Contains(t.Id) && t.DeletedAt != null)
            .Include(t => t.Tags)
            .Include(t => t.SubTasks)
            .AsSplitQuery()
            .ToListAsync(ct);

        if (tasks.Count == 0)
        {
            throw new NotFoundException("Deleted task");
        }

        var now = clock.GetUtcNow().UtcDateTime;
        foreach (var task in tasks)
        {
            task.DeletedAt = null;
            task.UpdatedAt = now;
        }

        await db.SaveChangesAsync(ct);
        return tasks.OrderBy(t => t.SortOrder).Select(t => t.ToDto()).ToList();
    }

    public async Task<int> BulkCompleteAsync(int userId, IReadOnlyList<int> ids, bool isCompleted, CancellationToken ct = default)
    {
        var tasks = await db.Tasks.Where(t => t.UserId == userId && ids.Contains(t.Id)).ToListAsync(ct);
        var now = clock.GetUtcNow().UtcDateTime;

        foreach (var task in tasks)
        {
            if (isCompleted)
            {
                ApplyStatus(task, TodoStatus.Completed, now);
            }
            else if (task.Status == TodoStatus.Completed)
            {
                ApplyStatus(task, TodoStatus.Pending, now);
            }

            task.UpdatedAt = now;
        }

        await db.SaveChangesAsync(ct);
        return tasks.Count;
    }

    public async Task<int> BulkDeleteAsync(int userId, IReadOnlyList<int> ids, CancellationToken ct = default)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var affected = await db.Tasks
            .Where(t => t.UserId == userId && ids.Contains(t.Id))
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.DeletedAt, (DateTime?)now)
                .SetProperty(t => t.UpdatedAt, now), ct);
        logger.LogInformation("User {UserId} deleted {Count} tasks", userId, affected);
        return affected;
    }

    // ----------------------------------------------------------------- helpers

    private static IOrderedQueryable<TodoTask> ApplySort(IQueryable<TodoTask> query, TaskSortBy sortBy, SortDirection? dir)
    {
        IOrderedQueryable<TodoTask> ordered;
        switch (sortBy)
        {
            case TaskSortBy.DueDate:
                // Tasks without a due date always sort last.
                ordered = dir == SortDirection.Desc
                    ? query.OrderBy(t => t.DueDate == null).ThenByDescending(t => t.DueDate)
                    : query.OrderBy(t => t.DueDate == null).ThenBy(t => t.DueDate);
                break;
            case TaskSortBy.Priority:
                ordered = dir == SortDirection.Asc
                    ? query.OrderBy(t => t.Priority)
                    : query.OrderByDescending(t => t.Priority);
                break;
            case TaskSortBy.Created:
                ordered = dir == SortDirection.Asc
                    ? query.OrderBy(t => t.CreatedAt)
                    : query.OrderByDescending(t => t.CreatedAt);
                break;
            case TaskSortBy.Title:
                ordered = dir == SortDirection.Desc
                    ? query.OrderByDescending(t => t.Title.ToLower())
                    : query.OrderBy(t => t.Title.ToLower());
                break;
            default:
                ordered = dir == SortDirection.Desc
                    ? query.OrderByDescending(t => t.Section.SortOrder).ThenByDescending(t => t.SortOrder)
                    : query.OrderBy(t => t.Section.SortOrder).ThenBy(t => t.SortOrder);
                break;
        }

        return ordered.ThenBy(t => t.Section.SortOrder).ThenBy(t => t.SortOrder).ThenBy(t => t.Id);
    }

    private async Task<TodoTask> LoadAsync(int userId, int id, bool tracking, CancellationToken ct)
    {
        var query = tracking ? db.Tasks.AsTracking() : db.Tasks.AsNoTracking();
        return await query
                   .Include(t => t.Tags)
                   .Include(t => t.SubTasks)
                   .AsSplitQuery()
                   .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId, ct)
               ?? throw new NotFoundException("Task");
    }

    private async Task EnsureSectionOwnedAsync(int userId, int sectionId, CancellationToken ct)
    {
        if (!await db.Sections.AnyAsync(s => s.Id == sectionId && s.UserId == userId, ct))
        {
            throw new BadRequestException("The selected section does not exist.", "sectionId");
        }
    }

    private async Task<List<Tag>> LoadTagsAsync(int userId, IReadOnlyCollection<int> tagIds, CancellationToken ct)
    {
        var ids = tagIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new List<Tag>();
        }

        var tags = await db.Tags.Where(t => t.UserId == userId && ids.Contains(t.Id)).ToListAsync(ct);
        if (tags.Count != ids.Count)
        {
            throw new BadRequestException("One or more tags do not exist.", "tagIds");
        }

        return tags;
    }

    private async Task<int> NextSortOrderAsync(int userId, int sectionId, CancellationToken ct) =>
        (await db.Tasks.Where(t => t.UserId == userId && t.SectionId == sectionId).MaxAsync(t => (int?)t.SortOrder, ct) ?? -1) + 1;

    private static void ApplyStatus(TodoTask task, TodoStatus status, DateTime now)
    {
        if (status == TodoStatus.Completed)
        {
            task.CompletedAt ??= now;
        }
        else
        {
            task.CompletedAt = null;
        }

        task.Status = status;
    }

    private void SyncSubTasks(TodoTask task, IReadOnlyList<SubTaskInput> inputs)
    {
        var existing = task.SubTasks.ToDictionary(s => s.Id);
        var kept = new HashSet<int>();
        var order = 0;

        foreach (var input in inputs)
        {
            if (input.Id is int subId && existing.TryGetValue(subId, out var sub))
            {
                sub.Title = input.Title.Trim();
                sub.IsCompleted = input.IsCompleted;
                sub.SortOrder = order++;
                kept.Add(subId);
            }
            else
            {
                task.SubTasks.Add(new SubTask { Title = input.Title.Trim(), IsCompleted = input.IsCompleted, SortOrder = order++ });
            }
        }

        foreach (var removed in task.SubTasks.Where(s => s.Id != 0 && !kept.Contains(s.Id)).ToList())
        {
            task.SubTasks.Remove(removed);
            db.SubTasks.Remove(removed);
        }
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string EscapeLike(string value) => value
        .Replace("\\", "\\\\")
        .Replace("%", "\\%")
        .Replace("_", "\\_")
        .Replace("[", "\\[");
}
