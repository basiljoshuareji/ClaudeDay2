using Microsoft.EntityFrameworkCore;
using TodoApp.Api.Data;
using TodoApp.Api.Domain;
using TodoApp.Api.Dtos;
using TodoApp.Api.Exceptions;
using TodoApp.Api.Mapping;

namespace TodoApp.Api.Services;

public class SubTaskService(AppDbContext db, TimeProvider clock) : ISubTaskService
{
    public async Task<SubTaskDto> AddAsync(int userId, int taskId, CreateSubTaskRequest request, CancellationToken ct = default)
    {
        var task = await db.Tasks.FirstOrDefaultAsync(t => t.Id == taskId && t.UserId == userId, ct)
                   ?? throw new NotFoundException("Task");

        var next = (await db.SubTasks.Where(s => s.TodoTaskId == taskId).MaxAsync(s => (int?)s.SortOrder, ct) ?? -1) + 1;
        var sub = new SubTask { TodoTaskId = taskId, Title = request.Title.Trim(), SortOrder = next };
        db.SubTasks.Add(sub);
        task.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);
        return sub.ToDto();
    }

    public async Task<SubTaskDto> UpdateAsync(int userId, int taskId, int id, UpdateSubTaskRequest request, CancellationToken ct = default)
    {
        var sub = await FindAsync(userId, taskId, id, ct);
        sub.Title = request.Title.Trim();
        sub.IsCompleted = request.IsCompleted;
        sub.TodoTask.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);
        return sub.ToDto();
    }

    public async Task DeleteAsync(int userId, int taskId, int id, CancellationToken ct = default)
    {
        var sub = await FindAsync(userId, taskId, id, ct);
        sub.TodoTask.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        db.SubTasks.Remove(sub);
        await db.SaveChangesAsync(ct);
    }

    private async Task<SubTask> FindAsync(int userId, int taskId, int id, CancellationToken ct) =>
        await db.SubTasks
            .Include(s => s.TodoTask)
            .FirstOrDefaultAsync(s => s.Id == id && s.TodoTaskId == taskId && s.TodoTask.UserId == userId, ct)
        ?? throw new NotFoundException("Sub-task");
}
