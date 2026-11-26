using TodoApp.Api.Domain;
using TodoApp.Api.Dtos;
using TodoApp.Api.Exceptions;
using TodoApp.Tests.TestSupport;

namespace TodoApp.Tests.Services;

public class TagServiceTests : IDisposable
{
    private readonly TestContext _t = new();

    public void Dispose() => _t.Dispose();

    [Fact]
    public async Task Tag_names_are_unique_per_user_ignoring_case_but_shared_across_users()
    {
        var (userA, _, _) = await _t.CreateUserAsync("a@example.com");
        var (userB, _, _) = await _t.CreateUserAsync("b@example.com");
        await _t.Tags.CreateAsync(userA, new TagRequest { Name = "Urgent", Color = "#ef4444" });

        await Assert.ThrowsAsync<ConflictException>(() => _t.Tags.CreateAsync(userA, new TagRequest { Name = "urgent", Color = "#000000" }));
        var other = await _t.Tags.CreateAsync(userB, new TagRequest { Name = "urgent", Color = "#000000" });
        Assert.Equal("urgent", other.Name);
    }

    [Fact]
    public async Task List_includes_task_counts_and_deleting_a_tag_detaches_it_from_tasks()
    {
        var (userId, homeId, _) = await _t.CreateUserAsync();
        var tag = await _t.Tags.CreateAsync(userId, new TagRequest { Name = "x", Color = "#123456" });
        var task = await _t.AddTaskAsync(userId, homeId, "tagged", tagIds: new List<int> { tag.Id });

        Assert.Equal(1, (await _t.Tags.GetAllAsync(userId)).Single().TaskCount);

        await _t.Tags.DeleteAsync(userId, tag.Id);
        _t.ClearTracking();

        Assert.Empty((await _t.Tasks.GetAsync(userId, task.Id)).Tags);
        Assert.Empty(await _t.Tags.GetAllAsync(userId));
    }

    [Fact]
    public async Task Tasks_cannot_use_another_users_tag()
    {
        var (userA, _, _) = await _t.CreateUserAsync("a@example.com");
        var (userB, homeB, _) = await _t.CreateUserAsync("b@example.com");
        var tag = await _t.Tags.CreateAsync(userA, new TagRequest { Name = "mine", Color = "#123456" });

        await Assert.ThrowsAsync<BadRequestException>(() => _t.AddTaskAsync(userB, homeB, "t", tagIds: new List<int> { tag.Id }));
        await Assert.ThrowsAsync<NotFoundException>(() => _t.Tags.DeleteAsync(userB, tag.Id));
    }
}

public class PreferenceServiceTests : IDisposable
{
    private readonly TestContext _t = new();

    public void Dispose() => _t.Dispose();

    [Theory]
    [InlineData("light")]
    [InlineData("dark")]
    [InlineData("ocean")]
    [InlineData("forest")]
    [InlineData("sunset")]
    public async Task Every_supported_theme_can_be_saved(string theme)
    {
        var (userId, _, _) = await _t.CreateUserAsync();

        var saved = await _t.Preferences.UpdateAsync(userId, new UpdatePreferencesRequest { Theme = theme, SidebarCollapsed = true });

        Assert.Equal(theme, saved.Theme);
        Assert.True((await _t.Preferences.GetAsync(userId)).SidebarCollapsed);
    }

    [Fact]
    public async Task Unknown_themes_are_rejected()
    {
        var (userId, _, _) = await _t.CreateUserAsync();

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => _t.Preferences.UpdateAsync(userId, new UpdatePreferencesRequest { Theme = "<script>" }));

        Assert.True(ex.Errors!.ContainsKey("theme"));
    }
}

public class SubTaskServiceTests : IDisposable
{
    private readonly TestContext _t = new();

    public void Dispose() => _t.Dispose();

    [Fact]
    public async Task Add_update_and_delete_a_checklist_item()
    {
        var (userId, homeId, _) = await _t.CreateUserAsync();
        var task = await _t.AddTaskAsync(userId, homeId, "parent");

        var first = await _t.SubTasks.AddAsync(userId, task.Id, new CreateSubTaskRequest { Title = "  one " });
        var second = await _t.SubTasks.AddAsync(userId, task.Id, new CreateSubTaskRequest { Title = "two" });
        Assert.Equal("one", first.Title);
        Assert.Equal(1, second.SortOrder);

        var updated = await _t.SubTasks.UpdateAsync(userId, task.Id, first.Id, new UpdateSubTaskRequest { Title = "uno", IsCompleted = true });
        Assert.True(updated.IsCompleted);

        await _t.SubTasks.DeleteAsync(userId, task.Id, second.Id);
        _t.ClearTracking();

        var reloaded = await _t.Tasks.GetAsync(userId, task.Id);
        Assert.Equal(new[] { "uno" }, reloaded.SubTasks.Select(s => s.Title).ToArray());
    }

    [Fact]
    public async Task Checklist_items_are_private_to_the_tasks_owner()
    {
        var (userA, homeA, _) = await _t.CreateUserAsync("a@example.com");
        var (userB, _, _) = await _t.CreateUserAsync("b@example.com");
        var task = await _t.AddTaskAsync(userA, homeA, "parent");
        var sub = await _t.SubTasks.AddAsync(userA, task.Id, new CreateSubTaskRequest { Title = "item" });

        await Assert.ThrowsAsync<NotFoundException>(() => _t.SubTasks.AddAsync(userB, task.Id, new CreateSubTaskRequest { Title = "x" }));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _t.SubTasks.UpdateAsync(userB, task.Id, sub.Id, new UpdateSubTaskRequest { Title = "x" }));
        await Assert.ThrowsAsync<NotFoundException>(() => _t.SubTasks.DeleteAsync(userB, task.Id, sub.Id));
    }

    [Fact]
    public async Task Sub_tasks_can_only_be_reached_through_their_own_task()
    {
        var (userId, homeId, _) = await _t.CreateUserAsync();
        var one = await _t.AddTaskAsync(userId, homeId, "one");
        var two = await _t.AddTaskAsync(userId, homeId, "two");
        var sub = await _t.SubTasks.AddAsync(userId, one.Id, new CreateSubTaskRequest { Title = "item" });

        await Assert.ThrowsAsync<NotFoundException>(() => _t.SubTasks.DeleteAsync(userId, two.Id, sub.Id));
    }
}
