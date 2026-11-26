using Microsoft.AspNetCore.Mvc;
using TodoApp.Api.Dtos;
using TodoApp.Api.Services;

namespace TodoApp.Api.Controllers;

[Route("api/sections")]
public class SectionsController(ISectionService sections) : ApiControllerBase
{
    /// <summary>Lists the user's sections with task counts (total / pending / in progress / completed).</summary>
    [HttpGet]
    public Task<IReadOnlyList<SectionDto>> List(CancellationToken ct) => sections.GetAllAsync(UserId, ct);

    [HttpGet("{id:int}")]
    public Task<SectionDto> Get(int id, CancellationToken ct) => sections.GetAsync(UserId, id, ct);

    [HttpPost]
    [ProducesResponseType(typeof(SectionDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<SectionDto>> Create(SectionRequest request, CancellationToken ct)
    {
        var section = await sections.CreateAsync(UserId, request, ct);
        return CreatedAtAction(nameof(Get), new { id = section.Id }, section);
    }

    [HttpPut("{id:int}")]
    public Task<SectionDto> Update(int id, SectionRequest request, CancellationToken ct) =>
        sections.UpdateAsync(UserId, id, request, ct);

    /// <summary>Deletes a custom section. Pass <c>moveTasksTo</c> to keep its tasks; otherwise they are deleted too.</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, [FromQuery] int? moveTasksTo, CancellationToken ct)
    {
        await sections.DeleteAsync(UserId, id, moveTasksTo, ct);
        return NoContent();
    }

    [HttpPut("reorder")]
    public Task<IReadOnlyList<SectionDto>> Reorder(ReorderRequest request, CancellationToken ct) =>
        sections.ReorderAsync(UserId, request.OrderedIds, ct);
}
