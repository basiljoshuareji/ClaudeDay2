using TodoApp.Api.Domain;
using TodoApp.Api.Dtos;
using TodoApp.Api.Exceptions;
using TodoApp.Tests.TestSupport;

namespace TodoApp.Tests.Services;

public class TaskServiceTests : IDisposable
{
    private readonly TestContext _t = new();

    public void Dispose() => _t.Dispose();

    private static string[] Titles(PagedResult<TaskDto> page) => page.Items.Select(i => i.Title).ToArray();

    // ----------------------------------------------------------------- CRUD

    [Fact]
    public async Task Create_sets_defaults_and_appends_to_the_end_of_the_section()
    {
        var (userId, homeId, _) = await _t.CreateUserAsync();

        var first = await _t.AddTaskAsync(userId, homeId, "  first  ");
        var second = await _t.AddTaskAsync(userId, homeId, "second");

        Assert.Equal("first", first.Title);
        Assert.Equal(TodoStatus.Pending, first.Status);
        Assert.Equal(TodoPriority.Medium, first.Priority);
        Assert.Null(first.CompletedAt);
        Assert.Equal(0, first.SortOrder);
        Assert.Equal(1, second.SortOrder);
        Assert.Equal(_t.Clock.Now.UtcDateTime, first.CreatedAt);
    }

    [Fact]
    public async Task Create_rejects_a_section_that_belongs_to_someone_else()
    {
        var (_, homeA, _) = await _t.CreateUserAsync("a@example.com");
        var (userB, _, _) = await _t.CreateUserAsync("b@example.com");

        var ex = await Assert.ThrowsAsync<BadRequestException>(() => _t.AddTaskAsync(userB, homeA, "sneaky"));
        Assert.True(ex.Errors!.ContainsKey("sectionId"));
    }

    [Fact]
    public async Task Create_can_include_subtasks_and_tags()
    {
        var (userId, homeId, _) = await _t.CreateUserAsync();
        var tag = await _t.Tags.CreateAsync(userId, new TagRequest { Name = "errand", Color = "#f59e0b" });

        var task = await _t.Tasks.CreateAsync(userId, new CreateTaskRequest
        {
            SectionId = homeId,
            Title = "Groceries",
            TagIds = new List<int> { tag.Id },
            SubTasks = new List<SubTaskInput>
            {
                new() { Title = "milk" },
                new() { Title = "eggs", IsCompleted = true }
            }
        });

        Assert.Equal(new[] { "errand" }, task.Tags.Select(t => t.Name).ToArray());
        Assert.Equal(new[] { "milk", "eggs" }, task.SubTasks.Select(s => s.Title).ToArray());
        Assert.True(task.SubTasks[1].IsCompleted);
    }

    [Fact]
    public async Task Update_changes_fields_tags_and_syncs_the_checklist()
    {
        var (userId, homeId, workId) = await _t.CreateUserAsync();
        var keep = await _t.Tags.CreateAsync(userId, new TagRequest { Name = "keep", Color = "#111111" });
        var drop = await _t.Tags.CreateAsync(userId, new TagRequest { Name = "drop", Color = "#222222" });
        var created = await _t.Tasks.CreateAsync(userId, new CreateTaskRequest
        {
            SectionId = homeId,
            Title = "Original",
            TagIds = new List<int> { drop.Id },
            SubTasks = new List<SubTaskInput> { new() { Title = "one" }, new() { Title = "two" } }
        });
        _t.Clock.Now = _t.Clock.Now.AddHours(1);

        var updated = await _t.Tasks.UpdateAsync(userId, created.Id, new UpdateTaskRequest
        {
            SectionId = workId,
            Title = "Renamed",
            Description = "  notes  ",
            Priority = TodoPriority.High,
            Status = TodoStatus.InProgress,
            DueDate = _t.Today.AddDays(3),
            TagIds = new List<int> { keep.Id },
            SubTasks = new List<SubTaskInput>
            {
                new() { Id = created.SubTasks[1].Id, Title = "two (edited)", IsCompleted = true },
                new() { Title = "three" }
            }
        });
        _t.ClearTracking();
        var reloaded = await _t.Tasks.GetAsync(userId, created.Id);

        Assert.Equal(workId, reloaded.SectionId);
        Assert.Equal("Renamed", reloaded.Title);
        Assert.Equal("notes", reloaded.Description);
        Assert.Equal(TodoPriority.High, reloaded.Priority);
        Assert.Equal(TodoStatus.InProgress, reloaded.Status);
        Assert.Equal(new[] { "keep" }, reloaded.Tags.Select(t => t.Name).ToArray());
        Assert.Equal(new[] { "two (edited)", "three" }, reloaded.SubTasks.Select(s => s.Title).ToArray());
        Assert.True(reloaded.SubTasks[0].IsCompleted);
        Assert.True(updated.UpdatedAt > created.UpdatedAt);
    }

    [Fact]
    public async Task Update_without_subtasks_leaves_the_checklist_alone()
    {
        var (userId, homeId, _) = await _t.CreateUserAsync();
        var created = await _t.Tasks.CreateAsync(userId, new CreateTaskRequest
        {
            SectionId = homeId,
            Title = "T",
            SubTasks = new List<SubTaskInput> { new() { Title = "one" } }
        });

        var updated = await _t.Tasks.UpdateAsync(userId, created.Id, new UpdateTaskRequest { SectionId = homeId, Title = "T2" });

        Assert.Single(updated.SubTasks);
    }

    [Fact]
    public async Task Tasks_are_private_to_their_owner()
    {
        var (userA, homeA, _) = await _t.CreateUserAsync("a@example.com");
        var (userB, homeB, _) = await _t.CreateUserAsync("b@example.com");
        var task = await _t.AddTaskAsync(userA, homeA, "secret");

        await Assert.ThrowsAsync<NotFoundException>(() => _t.Tasks.GetAsync(userB, task.Id));
        await Assert.ThrowsAsync<NotFoundException>(() => _t.Tasks.DeleteAsync(userB, task.Id));
        await Assert.ThrowsAsync<NotFoundException>(() => _t.Tasks.SetCompletionAsync(userB, task.Id, true));
        await Assert.ThrowsAsync<NotFoundException>(() => _t.Tasks.UpdateAsync(userB, task.Id,
            new UpdateTaskRequest { SectionId = homeB, Title = "x" }));
        Assert.Empty((await _t.Tasks.GetTasksAsync(userB, new TaskQuery())).Items);
    }

    // ------------------------------------------------------------ completion

    [Fact]
    public async Task Completing_and_uncompleting_sets_and_clears_completed_at()
    {
        var (userId, homeId, _) = await _t.CreateUserAsync();
        var task = await _t.AddTaskAsync(userId, homeId, "do it");

        var done = await _t.Tasks.SetCompletionAsync(userId, task.Id, true);
        Assert.Equal(TodoStatus.Completed, done.Status);
        Assert.Equal(_t.Clock.Now.UtcDateTime, done.CompletedAt);

        var undone = await _t.Tasks.SetCompletionAsync(userId, task.Id, false);
        Assert.Equal(TodoStatus.Pending, undone.Status);
        Assert.Null(undone.CompletedAt);
    }

    [Fact]
    public async Task Marking_incomplete_does_not_downgrade_an_in_progress_task()
    {
        var (userId, homeId, _) = await _t.CreateUserAsync();
        var task = await _t.AddTaskAsync(userId, homeId, "wip", status: TodoStatus.InProgress);

        var result = await _t.Tasks.SetCompletionAsync(userId, task.Id, false);

        Assert.Equal(TodoStatus.InProgress, result.Status);
    }

    // ------------------------------------------------------ search and filters

    [Fact]
    public async Task Search_matches_title_or_description_case_insensitively()
    {
        var (userId, homeId, _) = await _t.CreateUserAsync();
        await _t.AddTaskAsync(userId, homeId, "Buy MILK");
        await _t.AddTaskAsync(userId, homeId, "Call mum", description: "ask about the milk recipe");
        await _t.AddTaskAsync(userId, homeId, "Unrelated");

        var result = await _t.Tasks.GetTasksAsync(userId, new TaskQuery { Search = "milk" });

        Assert.Equal(new[] { "Buy MILK", "Call mum" }, Titles(result));
    }

    [Fact]
    public async Task Search_treats_wildcards_literally()
    {
        var (userId, homeId, _) = await _t.CreateUserAsync();
        await _t.AddTaskAsync(userId, homeId, "Save 50% on rent");
        await _t.AddTaskAsync(userId, homeId, "Save 500 on rent");
        await _t.AddTaskAsync(userId, homeId, "snake_case rename");
        await _t.AddTaskAsync(userId, homeId, "snakeXcase");

        Assert.Equal(new[] { "Save 50% on rent" }, Titles(await _t.Tasks.GetTasksAsync(userId, new TaskQuery { Search = "50%" })));
        Assert.Equal(new[] { "snake_case rename" }, Titles(await _t.Tasks.GetTasksAsync(userId, new TaskQuery { Search = "snake_case" })));
    }

    [Fact]
    public async Task Filters_by_status_priority_section_and_tag()
    {
        var (userId, homeId, workId) = await _t.CreateUserAsync();
        var urgent = await _t.Tags.CreateAsync(userId, new TagRequest { Name = "urgent", Color = "#ef4444" });
        await _t.AddTaskAsync(userId, homeId, "h-low-pending", TodoPriority.Low);
        await _t.AddTaskAsync(userId, homeId, "h-high-done", TodoPriority.High, status: TodoStatus.Completed);
        await _t.AddTaskAsync(userId, workId, "w-high-tagged", TodoPriority.High, tagIds: new List<int> { urgent.Id });

        Assert.Equal(new[] { "h-high-done" }, Titles(await _t.Tasks.GetTasksAsync(userId, new TaskQuery { Status = TodoStatus.Completed })));
        Assert.Equal(new[] { "h-high-done", "w-high-tagged" },
            Titles(await _t.Tasks.GetTasksAsync(userId, new TaskQuery { Priority = TodoPriority.High })));
        Assert.Equal(new[] { "w-high-tagged" }, Titles(await _t.Tasks.GetTasksAsync(userId, new TaskQuery { SectionId = workId })));
        Assert.Equal(new[] { "w-high-tagged" }, Titles(await _t.Tasks.GetTasksAsync(userId, new TaskQuery { TagId = urgent.Id })));
    }

    [Fact]
    public async Task Due_filters_split_tasks_into_today_upcoming_overdue_and_none()
    {
        var (userId, homeId, _) = await _t.CreateUserAsync();
        await _t.AddTaskAsync(userId, homeId, "overdue", due: _t.Today.AddDays(-2));
        await _t.AddTaskAsync(userId, homeId, "overdue-but-done", due: _t.Today.AddDays(-2), status: TodoStatus.Completed);
        await _t.AddTaskAsync(userId, homeId, "today", due: _t.Today);
        await _t.AddTaskAsync(userId, homeId, "upcoming", due: _t.Today.AddDays(5));
        await _t.AddTaskAsync(userId, homeId, "someday");

        Assert.Equal(new[] { "overdue" }, Titles(await _t.Tasks.GetTasksAsync(userId, new TaskQuery { Due = DueFilter.Overdue })));
        Assert.Equal(new[] { "today" }, Titles(await _t.Tasks.GetTasksAsync(userId, new TaskQuery { Due = DueFilter.Today })));
        Assert.Equal(new[] { "upcoming" }, Titles(await _t.Tasks.GetTasksAsync(userId, new TaskQuery { Due = DueFilter.Upcoming })));
        Assert.Equal(new[] { "someday" }, Titles(await _t.Tasks.GetTasksAsync(userId, new TaskQuery { Due = DueFilter.None })));
    }

    [Fact]
    public async Task Due_filters_use_the_callers_local_date_when_supplied()
    {
        var (userId, homeId, _) = await _t.CreateUserAsync();
        await _t.AddTaskAsync(userId, homeId, "due-tomorrow-utc", due: _t.Today.AddDays(1));

        var callerToday = _t.Today.AddDays(1); // e.g. a user far ahead of UTC
        var result = await _t.Tasks.GetTasksAsync(userId, new TaskQuery { Due = DueFilter.Today, Today = callerToday });

        Assert.Single(result.Items);
    }

    // ----------------------------------------------------------- sort and paging

    [Fact]
    public async Task Sorts_by_due_date_with_undated_tasks_last()
    {
        var (userId, homeId, _) = await _t.CreateUserAsync();
        await _t.AddTaskAsync(userId, homeId, "none");
        await _t.AddTaskAsync(userId, homeId, "later", due: _t.Today.AddDays(9));
        await _t.AddTaskAsync(userId, homeId, "sooner", due: _t.Today.AddDays(1));

        var asc = await _t.Tasks.GetTasksAsync(userId, new TaskQuery { SortBy = TaskSortBy.DueDate });
        var desc = await _t.Tasks.GetTasksAsync(userId, new TaskQuery { SortBy = TaskSortBy.DueDate, SortDir = SortDirection.Desc });

        Assert.Equal(new[] { "sooner", "later", "none" }, Titles(asc));
        Assert.Equal(new[] { "later", "sooner", "none" }, Titles(desc));
    }

    [Fact]
    public async Task Sorts_by_priority_high_first_by_default()
    {
        var (userId, homeId, _) = await _t.CreateUserAsync();
        await _t.AddTaskAsync(userId, homeId, "low", TodoPriority.Low);
        await _t.AddTaskAsync(userId, homeId, "high", TodoPriority.High);
        await _t.AddTaskAsync(userId, homeId, "medium", TodoPriority.Medium);

        var result = await _t.Tasks.GetTasksAsync(userId, new TaskQuery { SortBy = TaskSortBy.Priority });

        Assert.Equal(new[] { "high", "medium", "low" }, Titles(result));
    }

    [Fact]
    public async Task Sorts_by_title_ignoring_case_and_by_created_newest_first()
    {
        var (userId, homeId, _) = await _t.CreateUserAsync();
        await _t.AddTaskAsync(userId, homeId, "banana");
        _t.Clock.Now = _t.Clock.Now.AddMinutes(1);
        await _t.AddTaskAsync(userId, homeId, "Cherry");
        _t.Clock.Now = _t.Clock.Now.AddMinutes(1);
        await _t.AddTaskAsync(userId, homeId, "apple");

        Assert.Equal(new[] { "apple", "banana", "Cherry" },
            Titles(await _t.Tasks.GetTasksAsync(userId, new TaskQuery { SortBy = TaskSortBy.Title })));
        Assert.Equal(new[] { "apple", "Cherry", "banana" },
            Titles(await _t.Tasks.GetTasksAsync(userId, new TaskQuery { SortBy = TaskSortBy.Created })));
    }

    [Fact]
    public async Task Paginates_and_reports_totals()
    {
        var (userId, homeId, _) = await _t.CreateUserAsync();
        for (var i = 1; i <= 5; i++)
        {
            await _t.AddTaskAsync(userId, homeId, $"task {i}");
        }

        var page2 = await _t.Tasks.GetTasksAsync(userId, new TaskQuery { Page = 2, PageSize = 2 });

        Assert.Equal(new[] { "task 3", "task 4" }, Titles(page2));
        Assert.Equal(5, page2.TotalCount);
        Assert.Equal(3, page2.TotalPages);
    }

    // --------------------------------------------------- moving and reordering

    [Fact]
    public async Task Move_puts_the_task_at_the_end_of_the_target_section()
    {
        var (userId, homeId, workId) = await _t.CreateUserAsync();
        await _t.AddTaskAsync(userId, workId, "existing");
        var task = await _t.AddTaskAsync(userId, homeId, "traveller");

        var moved = await _t.Tasks.MoveAsync(userId, task.Id, workId);

        Assert.Equal(workId, moved.SectionId);
        Assert.Equal(1, moved.SortOrder);
    }

    [Fact]
    public async Task Reorder_applies_a_complete_new_order()
    {
        var (userId, homeId, _) = await _t.CreateUserAsync();
        var a = await _t.AddTaskAsync(userId, homeId, "a");
        var b = await _t.AddTaskAsync(userId, homeId, "b");
        var c = await _t.AddTaskAsync(userId, homeId, "c");

        await _t.Tasks.ReorderAsync(userId, new[] { c.Id, a.Id, b.Id });
        _t.ClearTracking();

        Assert.Equal(new[] { "c", "a", "b" }, Titles(await _t.Tasks.GetTasksAsync(userId, new TaskQuery { SectionId = homeId })));
    }

    [Fact]
    public async Task Reorder_of_a_partial_list_keeps_other_tasks_in_their_slots()
    {
        var (userId, homeId, _) = await _t.CreateUserAsync();
        var a = await _t.AddTaskAsync(userId, homeId, "a");
        await _t.AddTaskAsync(userId, homeId, "b");
        var c = await _t.AddTaskAsync(userId, homeId, "c");
        await _t.AddTaskAsync(userId, homeId, "d");

        // The user only sees a and c (filtered view) and swaps them.
        await _t.Tasks.ReorderAsync(userId, new[] { c.Id, a.Id });
        _t.ClearTracking();

        Assert.Equal(new[] { "c", "b", "a", "d" }, Titles(await _t.Tasks.GetTasksAsync(userId, new TaskQuery { SectionId = homeId })));
    }

    [Fact]
    public async Task Reorder_rejects_tasks_from_different_sections_and_foreign_ids()
    {
        var (userId, homeId, workId) = await _t.CreateUserAsync();
        var (otherUser, otherHome, _) = await _t.CreateUserAsync("other@example.com");
        var home = await _t.AddTaskAsync(userId, homeId, "home");
        var work = await _t.AddTaskAsync(userId, workId, "work");
        var foreign = await _t.AddTaskAsync(otherUser, otherHome, "foreign");

        await Assert.ThrowsAsync<BadRequestException>(() => _t.Tasks.ReorderAsync(userId, new[] { home.Id, work.Id }));
        await Assert.ThrowsAsync<BadRequestException>(() => _t.Tasks.ReorderAsync(userId, new[] { home.Id, foreign.Id }));
    }

    // ---------------------------------------------------- delete, undo, bulk

    [Fact]
    public async Task Delete_is_a_soft_delete_that_can_be_undone()
    {
        var (userId, homeId, _) = await _t.CreateUserAsync();
        var created = await _t.Tasks.CreateAsync(userId, new CreateTaskRequest
        {
            SectionId = homeId,
            Title = "Oops",
            SubTasks = new List<SubTaskInput> { new() { Title = "step" } }
        });

        await _t.Tasks.DeleteAsync(userId, created.Id);
        _t.ClearTracking();
        Assert.Empty((await _t.Tasks.GetTasksAsync(userId, new TaskQuery())).Items);
        await Assert.ThrowsAsync<NotFoundException>(() => _t.Tasks.GetAsync(userId, created.Id));

        var restored = await _t.Tasks.RestoreAsync(userId, new[] { created.Id });
        _t.ClearTracking();

        Assert.Single(restored);
        Assert.Single(restored[0].SubTasks);
        Assert.Single((await _t.Tasks.GetTasksAsync(userId, new TaskQuery())).Items);
    }

    [Fact]
    public async Task Restore_ignores_tasks_that_are_not_deleted_or_not_yours()
    {
        var (userA, homeA, _) = await _t.CreateUserAsync("a@example.com");
        var (userB, _, _) = await _t.CreateUserAsync("b@example.com");
        var task = await _t.AddTaskAsync(userA, homeA, "t");
        await _t.Tasks.DeleteAsync(userA, task.Id);

        await Assert.ThrowsAsync<NotFoundException>(() => _t.Tasks.RestoreAsync(userB, new[] { task.Id }));
    }

    [Fact]
    public async Task Bulk_complete_and_bulk_delete_only_touch_the_callers_tasks()
    {
        var (userA, homeA, _) = await _t.CreateUserAsync("a@example.com");
        var (userB, homeB, _) = await _t.CreateUserAsync("b@example.com");
        var mine1 = await _t.AddTaskAsync(userA, homeA, "mine 1");
        var mine2 = await _t.AddTaskAsync(userA, homeA, "mine 2");
        var theirs = await _t.AddTaskAsync(userB, homeB, "theirs");

        var completed = await _t.Tasks.BulkCompleteAsync(userA, new[] { mine1.Id, mine2.Id, theirs.Id }, true);
        _t.ClearTracking();
        Assert.Equal(2, completed);
        Assert.Equal(TodoStatus.Pending, (await _t.Tasks.GetAsync(userB, theirs.Id)).Status);
        Assert.All((await _t.Tasks.GetTasksAsync(userA, new TaskQuery())).Items, t => Assert.Equal(TodoStatus.Completed, t.Status));

        var deleted = await _t.Tasks.BulkDeleteAsync(userA, new[] { mine1.Id, theirs.Id });
        _t.ClearTracking();
        Assert.Equal(1, deleted);
        Assert.Equal(new[] { "mine 2" }, Titles(await _t.Tasks.GetTasksAsync(userA, new TaskQuery())));
        Assert.Single((await _t.Tasks.GetTasksAsync(userB, new TaskQuery())).Items);
    }
}
