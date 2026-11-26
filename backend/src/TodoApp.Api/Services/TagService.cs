using Microsoft.EntityFrameworkCore;
using TodoApp.Api.Data;
using TodoApp.Api.Domain;
using TodoApp.Api.Dtos;
using TodoApp.Api.Exceptions;

namespace TodoApp.Api.Services;

public class TagService(AppDbContext db) : ITagService
{
    public async Task<IReadOnlyList<TagListItemDto>> GetAllAsync(int userId, CancellationToken ct = default) =>
        await Project(db.Tags.Where(t => t.UserId == userId)).ToListAsync(ct);

    public async Task<TagListItemDto> CreateAsync(int userId, TagRequest request, CancellationToken ct = default)
    {
        var name = request.Name.Trim();
        await EnsureNameAvailableAsync(userId, name, null, ct);

        var tag = new Tag { UserId = userId, Name = name, Color = request.Color.ToLowerInvariant() };
        db.Tags.Add(tag);
        await db.SaveChangesAsync(ct);
        return new TagListItemDto(tag.Id, tag.Name, tag.Color, 0);
    }

    public async Task<TagListItemDto> UpdateAsync(int userId, int id, TagRequest request, CancellationToken ct = default)
    {
        var tag = await db.Tags.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId, ct)
                  ?? throw new NotFoundException("Tag");
        var name = request.Name.Trim();
        await EnsureNameAvailableAsync(userId, name, id, ct);

        tag.Name = name;
        tag.Color = request.Color.ToLowerInvariant();
        await db.SaveChangesAsync(ct);
        return await Project(db.Tags.Where(t => t.Id == id)).FirstAsync(ct);
    }

    public async Task DeleteAsync(int userId, int id, CancellationToken ct = default)
    {
        var tag = await db.Tags.FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId, ct)
                  ?? throw new NotFoundException("Tag");
        db.Tags.Remove(tag); // join rows are removed by cascade
        await db.SaveChangesAsync(ct);
    }

    private static IQueryable<TagListItemDto> Project(IQueryable<Tag> query) =>
        query.OrderBy(t => t.Name).Select(t => new TagListItemDto(t.Id, t.Name, t.Color, t.Tasks.Count()));

    private async Task EnsureNameAvailableAsync(int userId, string name, int? excludeId, CancellationToken ct)
    {
        var lower = name.ToLower();
        var exists = await db.Tags.AnyAsync(
            t => t.UserId == userId && t.Name.ToLower() == lower && (excludeId == null || t.Id != excludeId), ct);
        if (exists)
        {
            throw new ConflictException("You already have a tag with this name.", "name");
        }
    }
}
