using Microsoft.EntityFrameworkCore;
using TodoApp.Api.Data;
using TodoApp.Api.Domain;
using TodoApp.Api.Dtos;
using TodoApp.Api.Exceptions;
using TodoApp.Api.Mapping;

namespace TodoApp.Api.Services;

public class PreferenceService(AppDbContext db, TimeProvider clock) : IPreferenceService
{
    public async Task<PreferenceDto> GetAsync(int userId, CancellationToken ct = default) =>
        (await GetOrCreateAsync(userId, ct)).ToDto();

    public async Task<PreferenceDto> UpdateAsync(int userId, UpdatePreferencesRequest request, CancellationToken ct = default)
    {
        if (!ThemeNames.IsValid(request.Theme))
        {
            throw new BadRequestException($"Unknown theme. Valid themes: {string.Join(", ", ThemeNames.All)}.", "theme");
        }

        var preference = await GetOrCreateAsync(userId, ct);
        preference.Theme = request.Theme;
        preference.SidebarCollapsed = request.SidebarCollapsed;
        preference.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);
        return preference.ToDto();
    }

    private async Task<UserPreference> GetOrCreateAsync(int userId, CancellationToken ct)
    {
        var preference = await db.Preferences.FirstOrDefaultAsync(p => p.UserId == userId, ct);
        if (preference is not null)
        {
            return preference;
        }

        preference = new UserPreference { UserId = userId, UpdatedAt = clock.GetUtcNow().UtcDateTime };
        db.Preferences.Add(preference);
        await db.SaveChangesAsync(ct);
        return preference;
    }
}
