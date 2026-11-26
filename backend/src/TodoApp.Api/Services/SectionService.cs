using Microsoft.EntityFrameworkCore;
using TodoApp.Api.Data;
using TodoApp.Api.Domain;
using TodoApp.Api.Dtos;
using TodoApp.Api.Exceptions;

namespace TodoApp.Api.Services;

public class SectionService(AppDbContext db, TimeProvider clock, ILogger<SectionService> logger) : ISectionService
{
    public async Task<IReadOnlyList<SectionDto>> GetAllAsync(int userId, CancellationToken ct = default) =>
        await Project(db.Sections.Where(s => s.UserId == userId)).ToListAsync(ct);

    public async Task<SectionDto> GetAsync(int userId, int id, CancellationToken ct = default) =>
        await Project(db.Sections.Where(s => s.UserId == userId && s.Id == id)).FirstOrDefaultAsync(ct)
        ?? throw new NotFoundException("Section");

    public async Task<SectionDto> CreateAsync(int userId, SectionRequest request, CancellationToken ct = default)
    {
        var name = request.Name.Trim();
        await EnsureNameAvailableAsync(userId, name, null, ct);

        var next = (await db.Sections.Where(s => s.UserId == userId).MaxAsync(s => (int?)s.SortOrder, ct) ?? -1) + 1;
        var section = new Section
        {
            UserId = userId,
            Name = name,
            Color = request.Color.ToLowerInvariant(),
            Icon = request.Icon,
            SortOrder = next,
            CreatedAt = clock.GetUtcNow().UtcDateTime
        };

        db.Sections.Add(section);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("User {UserId} created section {SectionId}", userId, section.Id);
        return await GetAsync(userId, section.Id, ct);
    }

    public async Task<SectionDto> UpdateAsync(int userId, int id, SectionRequest request, CancellationToken ct = default)
    {
        var section = await FindAsync(userId, id, ct);
        var name = request.Name.Trim();
        await EnsureNameAvailableAsync(userId, name, id, ct);

        section.Name = name;
        section.Color = request.Color.ToLowerInvariant();
        section.Icon = request.Icon;
        await db.SaveChangesAsync(ct);
        return await GetAsync(userId, id, ct);
    }

    public async Task DeleteAsync(int userId, int id, int? moveTasksTo, CancellationToken ct = default)
    {
        var section = await FindAsync(userId, id, ct);
        if (section.IsDefault)
        {
            throw new BadRequestException("Default sections cannot be deleted.");
        }

        if (moveTasksTo is int targetId)
        {
            if (targetId == id)
            {
                throw new BadRequestException("Tasks cannot be moved into the section being deleted.", "moveTasksTo");
            }

            if (!await db.Sections.AnyAsync(s => s.Id == targetId && s.UserId == userId, ct))
            {
                throw new BadRequestException("The target section does not exist.", "moveTasksTo");
            }

            var nextOrder = (await db.Tasks.Where(t => t.SectionId == targetId).MaxAsync(t => (int?)t.SortOrder, ct) ?? -1) + 1;
            var tasks = await db.Tasks.Where(t => t.SectionId == id && t.UserId == userId)
                .OrderBy(t => t.SortOrder).ThenBy(t => t.Id)
                .ToListAsync(ct);
            foreach (var task in tasks)
            {
                task.SectionId = targetId;
                task.SortOrder = nextOrder++;
            }
        }

        // Remaining tasks (including soft-deleted ones) are removed by the database cascade.
        db.Sections.Remove(section);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("User {UserId} deleted section {SectionId}", userId, id);
    }

    public async Task<IReadOnlyList<SectionDto>> ReorderAsync(int userId, IReadOnlyList<int> orderedIds, CancellationToken ct = default)
    {
        var sections = await db.Sections.Where(s => s.UserId == userId).ToListAsync(ct);
        var byId = sections.ToDictionary(s => s.Id);
        if (orderedIds.Any(id => !byId.ContainsKey(id)))
        {
            throw new BadRequestException("One or more sections do not exist.", "orderedIds");
        }

        var listed = orderedIds.Distinct().ToList();
        var rest = sections.Where(s => !listed.Contains(s.Id)).OrderBy(s => s.SortOrder).ThenBy(s => s.Id).Select(s => s.Id);
        var order = 0;
        foreach (var id in listed.Concat(rest))
        {
            byId[id].SortOrder = order++;
        }

        await db.SaveChangesAsync(ct);
        return await GetAllAsync(userId, ct);
    }

    private static IQueryable<SectionDto> Project(IQueryable<Section> query) =>
        query.OrderBy(s => s.SortOrder).ThenBy(s => s.Id)
            .Select(s => new SectionDto(
                s.Id,
                s.Name,
                s.Color,
                s.Icon,
                s.SortOrder,
                s.IsDefault,
                s.Tasks.Count(),
                s.Tasks.Count(t => t.Status != TodoStatus.Completed),
                s.Tasks.Count(t => t.Status == TodoStatus.InProgress),
                s.Tasks.Count(t => t.Status == TodoStatus.Completed)));

    private async Task<Section> FindAsync(int userId, int id, CancellationToken ct) =>
        await db.Sections.FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId, ct)
        ?? throw new NotFoundException("Section");

    private async Task EnsureNameAvailableAsync(int userId, string name, int? excludeId, CancellationToken ct)
    {
        var lower = name.ToLower();
        var exists = await db.Sections.AnyAsync(
            s => s.UserId == userId && s.Name.ToLower() == lower && (excludeId == null || s.Id != excludeId), ct);
        if (exists)
        {
            throw new ConflictException("You already have a section with this name.", "name");
        }
    }
}
