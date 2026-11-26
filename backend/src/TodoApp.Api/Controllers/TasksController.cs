using Microsoft.AspNetCore.Mvc;
using TodoApp.Api.Dtos;
using TodoApp.Api.Services;

namespace TodoApp.Api.Controllers;

[Route("api/tasks")]
public class TasksController(ITaskService tasks) : ApiControllerBase
{
    /// <summary>Searches, filters, sorts and pages the user's tasks.</summary>
    [HttpGet]
    public Task<PagedResult<TaskDto>> List([FromQuery] TaskQuery query, CancellationToken ct) =>
        tasks.GetTasksAsync(UserId, query, ct);

    [HttpGet("{id:int}")]
    public Task<TaskDto> Get(int id, CancellationToken ct) => tasks.GetAsync(UserId, id, ct);

    [HttpPost]
    [ProducesResponseType(typeof(TaskDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<TaskDto>> Create(CreateTaskRequest request, CancellationToken ct)
    {
        var task = await tasks.CreateAsync(UserId, request, ct);
        return CreatedAtAction(nameof(Get), new { id = task.Id }, task);
    }

    [HttpPut("{id:int}")]
    public Task<TaskDto> Update(int id, UpdateTaskRequest request, CancellationToken ct) =>
        tasks.UpdateAsync(UserId, id, request, ct);

    /// <summary>Marks a task complete or incomplete.</summary>
    [HttpPatch("{id:int}/completion")]
    public Task<TaskDto> SetCompletion(int id, CompletionRequest request, CancellationToken ct) =>
        tasks.SetCompletionAsync(UserId, id, request.IsCompleted, ct);

    /// <summary>Moves a task to another section.</summary>
    [HttpPost("{id:int}/move")]
    public Task<TaskDto> Move(int id, MoveTaskRequest request, CancellationToken ct) =>
        tasks.MoveAsync(UserId, id, request.SectionId, ct);

    /// <summary>Soft-deletes a task. Use <c>POST /api/tasks/restore</c> to undo.</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await tasks.DeleteAsync(UserId, id, ct);
        return NoContent();
    }

    /// <summary>Applies a new manual order (drag and drop). Ids must belong to one section.</summary>
    [HttpPut("reorder")]
    public async Task<IActionResult> Reorder(ReorderRequest request, CancellationToken ct)
    {
        await tasks.ReorderAsync(UserId, request.OrderedIds, ct);
        return NoContent();
    }

    /// <summary>Restores recently deleted tasks (the "Undo" action).</summary>
    [HttpPost("restore")]
    public Task<IReadOnlyList<TaskDto>> Restore(BulkIdsRequest request, CancellationToken ct) =>
        tasks.RestoreAsync(UserId, request.Ids, ct);

    [HttpPost("bulk/complete")]
    public async Task<BulkResultDto> BulkComplete(BulkCompleteRequest request, CancellationToken ct) =>
        new(await tasks.BulkCompleteAsync(UserId, request.Ids, request.IsCompleted, ct));

    [HttpPost("bulk/delete")]
    public async Task<BulkResultDto> BulkDelete(BulkIdsRequest request, CancellationToken ct) =>
        new(await tasks.BulkDeleteAsync(UserId, request.Ids, ct));
}
