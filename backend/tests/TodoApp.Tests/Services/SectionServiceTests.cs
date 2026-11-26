using TodoApp.Api.Domain;
using TodoApp.Api.Dtos;
using TodoApp.Api.Exceptions;
using TodoApp.Tests.TestSupport;

namespace TodoApp.Tests.Services;

public class SectionServiceTests : IDisposable
{
    private readonly TestContext _t = new();

    public void Dispose() => _t.Dispose();

    private static SectionRequest Req(string name, string color = "#10b981", string icon = "star") =>
        new() { Name = name, Color = color, Icon = icon };

    [Fact]
    public async Task Counts_reflect_total_pending_in_progress_and_completed()
    {
        var (userId, homeId, _) = await _t.CreateUserAsync();
        await _t.AddTaskAsync(userId, homeId, "a");
        await _t.AddTaskAsync(userId, homeId, "b", status: TodoStatus.InProgress);
        await _t.AddTaskAsync(userId, homeId, "c", status: TodoStatus.Completed);

        var home = await _t.Sections.GetAsync(userId, homeId);

        Assert.Equal(3, home.TotalCount);
        Assert.Equal(2, home.PendingCount); // Pending + InProgress
        Assert.Equal(1, home.InProgressCount);
        Assert.Equal(1, home.CompletedCount);
    }

    [Fact]
    public async Task Soft_deleted_tasks_are_not_counted()
    {
        var (userId, homeId, _) = await _t.CreateUserAsync();
        var task = await _t.AddTaskAsync(userId, homeId, "gone");
        await _t.Tasks.DeleteAsync(userId, task.Id);

        Assert.Equal(0, (await _t.Sections.GetAsync(userId, homeId)).TotalCount);
    }

    [Fact]
    public async Task Create_appends_a_custom_section_and_rejects_duplicate_names()
    {
        var (userId, _, _) = await _t.CreateUserAsync();

        var created = await _t.Sections.CreateAsync(userId, Req("Fitness"));

        Assert.False(created.IsDefault);
        Assert.Equal(2, created.SortOrder);
        await Assert.ThrowsAsync<ConflictException>(() => _t.Sections.CreateAsync(userId, Req("fitness")));
    }

    [Fact]
    public async Task Update_renames_recolors_and_changes_icon()
    {
        var (userId, homeId, _) = await _t.CreateUserAsync();

        var updated = await _t.Sections.UpdateAsync(userId, homeId, Req("House", "#EF4444", "heart"));

        Assert.Equal("House", updated.Name);
        Assert.Equal("#ef4444", updated.Color);
        Assert.Equal("heart", updated.Icon);
        Assert.True(updated.IsDefault);
    }

    [Fact]
    public async Task Default_sections_cannot_be_deleted()
    {
        var (userId, homeId, _) = await _t.CreateUserAsync();

        await Assert.ThrowsAsync<BadRequestException>(() => _t.Sections.DeleteAsync(userId, homeId, null));
    }

    [Fact]
    public async Task Deleting_a_section_deletes_its_tasks()
    {
        var (userId, _, _) = await _t.CreateUserAsync();
        var custom = await _t.Sections.CreateAsync(userId, Req("Temp"));
        await _t.AddTaskAsync(userId, custom.Id, "doomed");

        await _t.Sections.DeleteAsync(userId, custom.Id, null);
        _t.ClearTracking();

        var all = await _t.Tasks.GetTasksAsync(userId, new TaskQuery());
        Assert.Empty(all.Items);
    }

    [Fact]
    public async Task Deleting_a_section_can_move_its_tasks_elsewhere()
    {
        var (userId, homeId, _) = await _t.CreateUserAsync();
        var custom = await _t.Sections.CreateAsync(userId, Req("Temp"));
        await _t.AddTaskAsync(userId, homeId, "existing");
        await _t.AddTaskAsync(userId, custom.Id, "moved-1");
        await _t.AddTaskAsync(userId, custom.Id, "moved-2");

        await _t.Sections.DeleteAsync(userId, custom.Id, moveTasksTo: homeId);
        _t.ClearTracking();

        var home = await _t.Tasks.GetTasksAsync(userId, new TaskQuery { SectionId = homeId });
        Assert.Equal(new[] { "existing", "moved-1", "moved-2" }, home.Items.Select(t => t.Title).ToArray());
    }

    [Fact]
    public async Task Sections_are_private_to_their_owner()
    {
        var (userA, homeA, _) = await _t.CreateUserAsync("a@example.com");
        var (userB, _, _) = await _t.CreateUserAsync("b@example.com");

        await Assert.ThrowsAsync<NotFoundException>(() => _t.Sections.GetAsync(userB, homeA));
        await Assert.ThrowsAsync<NotFoundException>(() => _t.Sections.UpdateAsync(userB, homeA, Req("hijack")));
        Assert.DoesNotContain(await _t.Sections.GetAllAsync(userB), s => s.Id == homeA);
        Assert.NotEmpty(await _t.Sections.GetAllAsync(userA));
    }

    [Fact]
    public async Task Reorder_applies_the_requested_order()
    {
        var (userId, homeId, workId) = await _t.CreateUserAsync();
        var custom = await _t.Sections.CreateAsync(userId, Req("Fitness"));

        var result = await _t.Sections.ReorderAsync(userId, new[] { custom.Id, workId, homeId });

        Assert.Equal(new[] { custom.Id, workId, homeId }, result.Select(s => s.Id).ToArray());
    }
}
