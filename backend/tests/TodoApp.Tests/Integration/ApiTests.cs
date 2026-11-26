using System.Net;
using System.Net.Http.Json;
using TodoApp.Api.Domain;
using TodoApp.Api.Dtos;

namespace TodoApp.Tests.Integration;

public class ApiTests : IClassFixture<ApiFactory>
{
    private readonly ApiFactory _factory;

    public ApiTests(ApiFactory factory) => _factory = factory;

    private static Task<T?> ReadAsync<T>(HttpResponseMessage response) =>
        response.Content.ReadFromJsonAsync<T>(ApiFactory.Json);

    private static async Task<SectionDto> SectionAsync(HttpClient client, string name)
    {
        var sections = (await client.GetFromJsonAsync<List<SectionDto>>("/api/sections", ApiFactory.Json))!;
        return sections.Single(s => s.Name == name);
    }

    // ------------------------------------------------------------------ auth

    [Fact]
    public async Task Registering_signs_the_user_in_and_creates_home_and_work_sections()
    {
        var (client, auth) = await _factory.CreateUserClientAsync();

        Assert.False(string.IsNullOrEmpty(auth.AccessToken));
        var sections = await client.GetFromJsonAsync<List<SectionDto>>("/api/sections", ApiFactory.Json);
        Assert.Equal(new[] { "Home", "Work" }, sections!.Select(s => s.Name).ToArray());
    }

    [Fact]
    public async Task Requests_without_a_token_get_a_consistent_401_error_body()
    {
        var response = await _factory.CreateClient().GetAsync("/api/tasks");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var error = await ReadAsync<ApiError>(response);
        Assert.Equal(401, error!.Status);
        Assert.Equal("unauthorized", error.Code);
        Assert.False(string.IsNullOrEmpty(error.TraceId));
    }

    [Fact]
    public async Task A_garbage_token_is_rejected()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", "not.a.jwt");

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/sections")).StatusCode);
    }

    [Fact]
    public async Task Login_works_and_a_wrong_password_returns_401()
    {
        var email = $"login-{Guid.NewGuid():N}@example.com";
        await _factory.CreateUserClientAsync(email);
        var client = _factory.CreateClient();

        var ok = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = email, Password = "Passw0rd!" });
        var bad = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Email = email, Password = "nope-nope-1" });

        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, bad.StatusCode);
    }

    [Fact]
    public async Task Invalid_registration_returns_field_level_validation_errors()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/auth/register",
            new RegisterRequest { Email = "not-an-email", Password = "short", DisplayName = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await ReadAsync<ApiError>(response);
        Assert.Equal("validation_failed", error!.Code);
        Assert.Contains("email", error.Errors!.Keys);
        Assert.Contains("password", error.Errors.Keys);
        Assert.Contains("displayName", error.Errors.Keys);
    }

    [Fact]
    public async Task Registering_the_same_email_twice_returns_409()
    {
        var email = $"dupe-{Guid.NewGuid():N}@example.com";
        await _factory.CreateUserClientAsync(email);

        var response = await _factory.CreateClient().PostAsJsonAsync("/api/auth/register",
            new RegisterRequest { Email = email, Password = "Passw0rd!", DisplayName = "Again" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("conflict", (await ReadAsync<ApiError>(response))!.Code);
    }

    // ----------------------------------------------------------------- tasks

    [Fact]
    public async Task Full_task_lifecycle_create_filter_complete_delete_and_undo()
    {
        var (client, _) = await _factory.CreateUserClientAsync();
        var home = await SectionAsync(client, "Home");

        var create = await client.PostAsJsonAsync("/api/tasks", new CreateTaskRequest
        {
            SectionId = home.Id,
            Title = "Pay rent",
            Priority = TodoPriority.High,
            DueDate = new DateOnly(2030, 1, 1)
        }, ApiFactory.Json);
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var task = (await ReadAsync<TaskDto>(create))!;
        Assert.Equal(TodoPriority.High, task.Priority);
        Assert.Equal(new DateOnly(2030, 1, 1), task.DueDate);

        var list = (await client.GetFromJsonAsync<PagedResult<TaskDto>>("/api/tasks?search=rent&priority=High&sortBy=DueDate", ApiFactory.Json))!;
        Assert.Single(list.Items);

        var complete = await client.PatchAsJsonAsync($"/api/tasks/{task.Id}/completion", new CompletionRequest { IsCompleted = true });
        Assert.Equal(TodoStatus.Completed, (await ReadAsync<TaskDto>(complete))!.Status);

        var delete = await client.DeleteAsync($"/api/tasks/{task.Id}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/tasks/{task.Id}")).StatusCode);

        var restore = await client.PostAsJsonAsync("/api/tasks/restore", new BulkIdsRequest { Ids = new List<int> { task.Id } });
        Assert.Equal(HttpStatusCode.OK, restore.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/tasks/{task.Id}")).StatusCode);
    }

    [Fact]
    public async Task Invalid_task_returns_400_with_field_errors()
    {
        var (client, _) = await _factory.CreateUserClientAsync();

        var response = await client.PostAsJsonAsync("/api/tasks", new { sectionId = 0, title = "" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await ReadAsync<ApiError>(response);
        Assert.Contains("title", error!.Errors!.Keys);
        Assert.Contains("sectionId", error.Errors.Keys);
    }

    [Fact]
    public async Task Bad_query_values_return_a_400_instead_of_a_500()
    {
        var (client, _) = await _factory.CreateUserClientAsync();

        var response = await client.GetAsync("/api/tasks?status=Banana&pageSize=9999");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Users_cannot_see_or_change_each_others_data()
    {
        var (alice, _) = await _factory.CreateUserClientAsync();
        var (bob, _) = await _factory.CreateUserClientAsync();
        var aliceHome = await SectionAsync(alice, "Home");
        var task = (await ReadAsync<TaskDto>(await alice.PostAsJsonAsync("/api/tasks",
            new CreateTaskRequest { SectionId = aliceHome.Id, Title = "Alice's secret" }, ApiFactory.Json)))!;

        Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync($"/api/tasks/{task.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.DeleteAsync($"/api/tasks/{task.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await bob.GetAsync($"/api/sections/{aliceHome.Id}")).StatusCode);

        var bobList = (await bob.GetFromJsonAsync<PagedResult<TaskDto>>("/api/tasks", ApiFactory.Json))!;
        Assert.Empty(bobList.Items);

        // Bob cannot drop a task into Alice's section either.
        var bobHome = await SectionAsync(bob, "Home");
        var bobTask = (await ReadAsync<TaskDto>(await bob.PostAsJsonAsync("/api/tasks",
            new CreateTaskRequest { SectionId = bobHome.Id, Title = "Bob's" }, ApiFactory.Json)))!;
        var move = await bob.PostAsJsonAsync($"/api/tasks/{bobTask.Id}/move", new MoveTaskRequest { SectionId = aliceHome.Id });
        Assert.Equal(HttpStatusCode.BadRequest, move.StatusCode);
    }

    [Fact]
    public async Task Sub_tasks_tags_and_moving_between_sections_work_over_http()
    {
        var (client, _) = await _factory.CreateUserClientAsync();
        var home = await SectionAsync(client, "Home");
        var work = await SectionAsync(client, "Work");

        var tagResponse = await client.PostAsJsonAsync("/api/tags", new TagRequest { Name = "focus", Color = "#0ea5e9" });
        Assert.Equal(HttpStatusCode.Created, tagResponse.StatusCode);
        var tag = (await ReadAsync<TagListItemDto>(tagResponse))!;

        var task = (await ReadAsync<TaskDto>(await client.PostAsJsonAsync("/api/tasks",
            new CreateTaskRequest { SectionId = home.Id, Title = "Plan", TagIds = new List<int> { tag.Id } }, ApiFactory.Json)))!;

        var subResponse = await client.PostAsJsonAsync($"/api/tasks/{task.Id}/subtasks", new CreateSubTaskRequest { Title = "outline" });
        Assert.Equal(HttpStatusCode.Created, subResponse.StatusCode);
        var sub = (await ReadAsync<SubTaskDto>(subResponse))!;
        var toggled = await client.PutAsJsonAsync($"/api/tasks/{task.Id}/subtasks/{sub.Id}",
            new UpdateSubTaskRequest { Title = "outline", IsCompleted = true });
        Assert.True((await ReadAsync<SubTaskDto>(toggled))!.IsCompleted);

        var moved = (await ReadAsync<TaskDto>(await client.PostAsJsonAsync($"/api/tasks/{task.Id}/move",
            new MoveTaskRequest { SectionId = work.Id })))!;
        Assert.Equal(work.Id, moved.SectionId);

        var byTag = (await client.GetFromJsonAsync<PagedResult<TaskDto>>($"/api/tasks?tagId={tag.Id}", ApiFactory.Json))!;
        Assert.Equal(task.Id, byTag.Items.Single().Id);
        Assert.Single(byTag.Items[0].SubTasks);

        var counts = await SectionAsync(client, "Work");
        Assert.Equal(1, counts.TotalCount);
    }

    [Fact]
    public async Task Bulk_endpoints_complete_and_delete_selected_tasks()
    {
        var (client, _) = await _factory.CreateUserClientAsync();
        var home = await SectionAsync(client, "Home");
        var ids = new List<int>();
        foreach (var title in new[] { "a", "b", "c" })
        {
            var created = (await ReadAsync<TaskDto>(await client.PostAsJsonAsync("/api/tasks",
                new CreateTaskRequest { SectionId = home.Id, Title = title }, ApiFactory.Json)))!;
            ids.Add(created.Id);
        }

        var complete = await client.PostAsJsonAsync("/api/tasks/bulk/complete", new BulkCompleteRequest { Ids = ids.Take(2).ToList() });
        Assert.Equal(2, (await ReadAsync<BulkResultDto>(complete))!.Affected);

        var delete = await client.PostAsJsonAsync("/api/tasks/bulk/delete", new BulkIdsRequest { Ids = ids.Take(2).ToList() });
        Assert.Equal(2, (await ReadAsync<BulkResultDto>(delete))!.Affected);

        var remaining = (await client.GetFromJsonAsync<PagedResult<TaskDto>>("/api/tasks", ApiFactory.Json))!;
        Assert.Equal(new[] { "c" }, remaining.Items.Select(t => t.Title).ToArray());
    }

    [Fact]
    public async Task Reordering_over_http_changes_the_manual_order()
    {
        var (client, _) = await _factory.CreateUserClientAsync();
        var home = await SectionAsync(client, "Home");
        var ids = new List<int>();
        foreach (var title in new[] { "one", "two", "three" })
        {
            ids.Add((await ReadAsync<TaskDto>(await client.PostAsJsonAsync("/api/tasks",
                new CreateTaskRequest { SectionId = home.Id, Title = title }, ApiFactory.Json)))!.Id);
        }

        var reorder = await client.PutAsJsonAsync("/api/tasks/reorder", new ReorderRequest { OrderedIds = new List<int> { ids[2], ids[0], ids[1] } });
        Assert.Equal(HttpStatusCode.NoContent, reorder.StatusCode);

        var list = (await client.GetFromJsonAsync<PagedResult<TaskDto>>($"/api/tasks?sectionId={home.Id}", ApiFactory.Json))!;
        Assert.Equal(new[] { "three", "one", "two" }, list.Items.Select(t => t.Title).ToArray());
    }

    // --------------------------------------------------- sections and preferences

    [Fact]
    public async Task Custom_sections_can_be_created_renamed_and_deleted_but_defaults_cannot()
    {
        var (client, _) = await _factory.CreateUserClientAsync();
        var home = await SectionAsync(client, "Home");

        var create = await client.PostAsJsonAsync("/api/sections", new SectionRequest { Name = "Fitness", Color = "#10b981", Icon = "heart" });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var section = (await ReadAsync<SectionDto>(create))!;

        var rename = await client.PutAsJsonAsync($"/api/sections/{section.Id}", new SectionRequest { Name = "Gym", Color = "#ef4444", Icon = "star" });
        Assert.Equal("Gym", (await ReadAsync<SectionDto>(rename))!.Name);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/api/sections/{section.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.DeleteAsync($"/api/sections/{home.Id}")).StatusCode);

        var badColor = await client.PostAsJsonAsync("/api/sections", new SectionRequest { Name = "X", Color = "red", Icon = "star" });
        Assert.Equal(HttpStatusCode.BadRequest, badColor.StatusCode);
    }

    [Fact]
    public async Task Theme_preference_persists_and_is_returned_by_me()
    {
        var (client, _) = await _factory.CreateUserClientAsync();

        var save = await client.PutAsJsonAsync("/api/preferences", new UpdatePreferencesRequest { Theme = "sunset", SidebarCollapsed = true });
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);

        var me = (await client.GetFromJsonAsync<SessionDto>("/api/auth/me", ApiFactory.Json))!;
        Assert.Equal("sunset", me.Preferences.Theme);
        Assert.True(me.Preferences.SidebarCollapsed);

        var bad = await client.PutAsJsonAsync("/api/preferences", new UpdatePreferencesRequest { Theme = "rainbow" });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
    }

    [Fact]
    public async Task Health_endpoint_is_public_and_swagger_document_is_served()
    {
        var client = _factory.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/health")).StatusCode);
        var swagger = await client.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, swagger.StatusCode);
        Assert.Contains("/api/tasks", await swagger.Content.ReadAsStringAsync());
    }
}
