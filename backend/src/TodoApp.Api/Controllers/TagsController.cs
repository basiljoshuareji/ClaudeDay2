using Microsoft.AspNetCore.Mvc;
using TodoApp.Api.Dtos;
using TodoApp.Api.Services;

namespace TodoApp.Api.Controllers;

[Route("api/tags")]
public class TagsController(ITagService tags) : ApiControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<TagListItemDto>> List(CancellationToken ct) => tags.GetAllAsync(UserId, ct);

    [HttpPost]
    [ProducesResponseType(typeof(TagListItemDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<TagListItemDto>> Create(TagRequest request, CancellationToken ct)
    {
        var tag = await tags.CreateAsync(UserId, request, ct);
        return StatusCode(StatusCodes.Status201Created, tag);
    }

    [HttpPut("{id:int}")]
    public Task<TagListItemDto> Update(int id, TagRequest request, CancellationToken ct) =>
        tags.UpdateAsync(UserId, id, request, ct);

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await tags.DeleteAsync(UserId, id, ct);
        return NoContent();
    }
}
