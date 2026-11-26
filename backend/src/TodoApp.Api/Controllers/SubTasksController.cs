using Microsoft.AspNetCore.Mvc;
using TodoApp.Api.Dtos;
using TodoApp.Api.Services;

namespace TodoApp.Api.Controllers;

[Route("api/tasks/{taskId:int}/subtasks")]
public class SubTasksController(ISubTaskService subTasks) : ApiControllerBase
{
    [HttpPost]
    [ProducesResponseType(typeof(SubTaskDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<SubTaskDto>> Add(int taskId, CreateSubTaskRequest request, CancellationToken ct)
    {
        var sub = await subTasks.AddAsync(UserId, taskId, request, ct);
        return StatusCode(StatusCodes.Status201Created, sub);
    }

    [HttpPut("{id:int}")]
    public Task<SubTaskDto> Update(int taskId, int id, UpdateSubTaskRequest request, CancellationToken ct) =>
        subTasks.UpdateAsync(UserId, taskId, id, request, ct);

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int taskId, int id, CancellationToken ct)
    {
        await subTasks.DeleteAsync(UserId, taskId, id, ct);
        return NoContent();
    }
}
