using System.Net;
using TodoMcpServer.Api;
using TodoMcpServer.Tests.TestSupport;

namespace TodoMcpServer.Tests;

public class TodoApiClientTests
{
    // ----------------------------------------------------------- section-name resolution

    [Theory]
    [InlineData("Home", 1)]
    [InlineData("home", 1)]
    [InlineData("  WORK  ", 2)]
    [InlineData("fItNeSs", 3)]
    public async Task Section_names_resolve_case_insensitively_and_ignore_surrounding_whitespace(string typed, int expectedId)
    {
        var client = new FakeTodoApi().CreateClient();

        var section = await client.ResolveSectionAsync(typed);

        Assert.Equal(expectedId, section.Id);
    }

    [Fact]
    public async Task An_unknown_section_error_lists_the_sections_that_exist()
    {
        var client = new FakeTodoApi().CreateClient();

        var ex = await Assert.ThrowsAsync<SectionNotFoundException>(() => client.ResolveSectionAsync("Gym"));

        Assert.Equal("Section 'Gym' not found. Available sections: Home, Work, Fitness.", ex.Message);
    }

    [Fact]
    public async Task A_blank_section_is_reported_as_not_found_rather_than_guessed()
    {
        var client = new FakeTodoApi().CreateClient();

        await Assert.ThrowsAsync<SectionNotFoundException>(() => client.ResolveSectionAsync("   "));
    }

    // -------------------------------------------------------------------------- auth

    [Fact]
    public async Task It_signs_in_once_and_reuses_the_cached_token()
    {
        var api = new FakeTodoApi();
        var client = api.CreateClient();

        await client.GetSectionsAsync();
        await client.GetSectionsAsync();
        await client.GetTagsAsync();

        Assert.Equal(1, api.LoginCount);
        Assert.All(api.Requests.Where(r => r.Path != "/api/auth/login"), r => Assert.Equal("Bearer token-1", r.Authorization));
    }

    [Fact]
    public async Task The_login_request_uses_the_configured_email_and_password()
    {
        var api = new FakeTodoApi();

        await api.CreateClient().GetSectionsAsync();

        var login = api.Requests.Single(r => r.Path == "/api/auth/login");
        Assert.Equal(HttpMethod.Post, login.Method);
        Assert.Contains("\"email\":\"ada@example.com\"", login.Body);
        Assert.Contains("\"password\":\"Passw0rd1\"", login.Body);
        Assert.Null(login.Authorization); // login itself is anonymous
    }

    [Fact]
    public async Task A_401_triggers_exactly_one_re_login_and_the_request_is_retried()
    {
        var api = new FakeTodoApi();
        var client = api.CreateClient();
        await client.GetSectionsAsync();                 // signs in -> token-1
        api.AcceptedToken = "revoked-elsewhere";         // the API stops accepting token-1

        var sections = await client.GetSectionsAsync();  // 401 -> re-login (token-2) -> retry succeeds

        Assert.Equal(3, sections.Count);
        Assert.Equal(2, api.LoginCount);
        var gets = api.Requests.Where(r => r.Path == "/api/sections").ToList();
        Assert.Equal(3, gets.Count); // first ok, second rejected, third retried
        Assert.Equal("Bearer token-1", gets[1].Authorization);
        Assert.Equal("Bearer token-2", gets[2].Authorization);
    }

    [Fact]
    public async Task A_persistent_401_gives_up_after_one_re_login_instead_of_looping()
    {
        var api = new FakeTodoApi
        {
            Override = r => r.Path == "/api/sections"
                ? FakeTodoApi.ApiError(HttpStatusCode.Unauthorized, "unauthorized", "Authentication is required or the token is invalid or expired.")
                : null,
        };
        var client = api.CreateClient();

        var ex = await Assert.ThrowsAsync<TodoApiException>(() => client.GetSectionsAsync());

        Assert.Equal(HttpStatusCode.Unauthorized, ex.StatusCode);
        Assert.Equal(2, api.LoginCount);                                              // initial + one re-login, never more
        Assert.Equal(2, api.Requests.Count(r => r.Path == "/api/sections"));          // original + one retry
    }

    [Fact]
    public async Task Rejected_credentials_raise_an_authentication_error_that_never_contains_the_password()
    {
        var api = new FakeTodoApi { LoginSucceeds = false };
        var client = api.CreateClient();

        var ex = await Assert.ThrowsAsync<TodoAuthenticationException>(() => client.GetSectionsAsync());

        Assert.Contains("ada@example.com", ex.Message);
        Assert.DoesNotContain("Passw0rd1", ex.Message);
        Assert.Equal(1, api.LoginCount);
    }

    [Fact]
    public async Task An_expired_cached_token_is_refreshed_before_it_is_used()
    {
        var clock = new FakeClock(new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero));
        var api = new FakeTodoApi
        {
            // Hand out tokens that "expire" at a fixed instant relative to the fake clock.
            Override = r => r.Path == "/api/auth/login" ? LoginResponse(api: null!, clock) : null,
        };
        api.Override = r => r.Path == "/api/auth/login" ? LoginResponse(api, clock) : null;
        var client = api.CreateClient(clock);

        await client.GetSectionsAsync();
        clock.Now = clock.Now.AddHours(3); // past the 2h token lifetime
        await client.GetSectionsAsync();

        Assert.Equal(2, api.LoginCount);
        Assert.Equal(2, api.Requests.Count(r => r.Path == "/api/sections"));   // no wasted 401 round-trip
    }

    private static HttpResponseMessage LoginResponse(FakeTodoApi api, FakeClock clock)
    {
        var token = $"token-{api.Requests.Count(r => r.Path == "/api/auth/login")}";
        api.AcceptedToken = token;
        return FakeTodoApi.JsonResponse(HttpStatusCode.OK, new { accessToken = token, expiresAt = clock.Now.AddHours(2).UtcDateTime.ToString("o") });
    }

    // ------------------------------------------------------------------ error handling

    [Fact]
    public async Task Validation_errors_from_the_api_become_readable_messages_with_field_details()
    {
        var api = new FakeTodoApi
        {
            Override = r => r.Method == HttpMethod.Post && r.Path == "/api/tasks"
                ? FakeTodoApi.ApiError(HttpStatusCode.BadRequest, "validation_failed", "One or more validation errors occurred.",
                    new Dictionary<string, string[]> { ["title"] = new[] { "The Title field is required." } })
                : null,
        };
        var client = api.CreateClient();

        var ex = await Assert.ThrowsAsync<TodoApiException>(() =>
            client.CreateTaskAsync(new CreateTaskPayload(1, "", null, null, TodoPriority.Medium, TodoStatus.Pending, Array.Empty<int>(), Array.Empty<SubTaskPayload>())));

        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
        Assert.Contains("One or more validation errors occurred.", ex.Message);
        Assert.Contains("title: The Title field is required.", ex.Message);
    }

    [Fact]
    public async Task An_unreachable_api_raises_a_clear_unavailable_error()
    {
        var api = new FakeTodoApi { Override = _ => throw new HttpRequestException("Connection refused") };
        var client = api.CreateClient();

        var ex = await Assert.ThrowsAsync<TodoApiUnavailableException>(() => client.GetSectionsAsync());

        Assert.Contains("Could not reach the Todo API at http://api.test", ex.Message);
        Assert.Contains("Is it running?", ex.Message);
    }

    [Fact]
    public async Task A_timeout_is_reported_as_unavailable_not_as_a_cancellation()
    {
        var api = new FakeTodoApi { Override = _ => throw new TaskCanceledException("The request timed out.") };
        var client = api.CreateClient();

        var ex = await Assert.ThrowsAsync<TodoApiUnavailableException>(() => client.GetSectionsAsync());

        Assert.Contains("did not respond within 30 seconds", ex.Message);
    }

    [Fact]
    public async Task A_cancelled_call_stays_a_cancellation()
    {
        var client = new FakeTodoApi().CreateClient();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetSectionsAsync(cts.Token));
    }

    [Fact]
    public async Task A_non_api_error_page_still_yields_a_sensible_message()
    {
        var api = new FakeTodoApi
        {
            Override = r => r.Path == "/api/sections"
                ? new HttpResponseMessage(HttpStatusCode.BadGateway) { Content = new StringContent("<html>Bad gateway</html>") }
                : null,
        };

        var ex = await Assert.ThrowsAsync<TodoApiException>(() => api.CreateClient().GetSectionsAsync());

        Assert.Equal(HttpStatusCode.BadGateway, ex.StatusCode);
        Assert.Contains("HTTP 502", ex.Message);
    }

    // ------------------------------------------------------------------ duplicate guard

    [Fact]
    public async Task The_duplicate_guard_finds_an_open_task_with_the_same_title_ignoring_case_and_spacing()
    {
        var api = new FakeTodoApi();
        api.Tasks.Add(FakeTodoApi.TaskJson(7, "Fix the   Leaking tap", sectionId: 1));
        api.Tasks.Add(FakeTodoApi.TaskJson(8, "Something else", sectionId: 1));
        var client = api.CreateClient();

        var duplicate = await client.FindOpenDuplicateAsync(1, "  fix the leaking TAP ");

        Assert.NotNull(duplicate);
        Assert.Equal(7, duplicate!.Id);
        var query = api.Requests.Last(r => r.Path == "/api/tasks").PathAndQuery;
        Assert.Contains("sectionId=1", query);
        Assert.Contains("search=fix%20the%20leaking%20TAP", query);
    }

    [Fact]
    public async Task The_duplicate_guard_ignores_completed_tasks_and_partial_title_matches()
    {
        var api = new FakeTodoApi();
        api.Tasks.Add(FakeTodoApi.TaskJson(1, "Buy milk", status: "Completed"));
        api.Tasks.Add(FakeTodoApi.TaskJson(2, "Buy milk and eggs"));
        var client = api.CreateClient();

        Assert.Null(await client.FindOpenDuplicateAsync(1, "Buy milk"));
    }

    [Fact]
    public async Task The_duplicate_guard_treats_in_progress_tasks_as_open()
    {
        var api = new FakeTodoApi();
        api.Tasks.Add(FakeTodoApi.TaskJson(3, "Write report", status: "InProgress"));

        var duplicate = await api.CreateClient().FindOpenDuplicateAsync(1, "write report");

        Assert.Equal(3, duplicate?.Id);
    }

    // ----------------------------------------------------------------------- tags

    [Fact]
    public async Task Tag_names_reuse_existing_tags_and_create_missing_ones()
    {
        var api = new FakeTodoApi();
        api.Tags.Add(new { id = 5, name = "Urgent", color = "#ef4444", taskCount = 2 });
        var client = api.CreateClient();

        var (ids, created) = await client.ResolveTagIdsAsync(new[] { "urgent", "URGENT", " plumbing " }, createMissing: true);

        Assert.Equal(2, ids.Count);
        Assert.Contains(5, ids);
        Assert.Equal(new[] { "plumbing" }, created);
        Assert.Single(api.Requests, r => r.Method == HttpMethod.Post && r.Path == "/api/tags");
    }

    [Fact]
    public async Task Filtering_by_an_unknown_tag_lists_the_tags_that_exist()
    {
        var api = new FakeTodoApi();
        api.Tags.Add(new { id = 5, name = "Urgent", color = "#ef4444", taskCount = 2 });

        var ex = await Assert.ThrowsAsync<TodoLookupException>(() =>
            api.CreateClient().ResolveTagIdsAsync(new[] { "nope" }, createMissing: false));

        Assert.Equal("Tag 'nope' not found. Available tags: Urgent.", ex.Message);
    }

    // ----------------------------------------------------------------- requests sent

    [Fact]
    public async Task Listing_tasks_sends_the_filters_and_the_users_local_date()
    {
        var api = new FakeTodoApi();
        var client = api.CreateClient();

        await client.ListTasksAsync(new TaskListQuery(SectionId: 2, Status: TodoStatus.Pending, Due: TodoDueFilter.Overdue, TagId: 9, Search: "tap & sink", PageSize: 20));

        var query = api.Requests.Last(r => r.Path == "/api/tasks").PathAndQuery;
        Assert.Contains("sectionId=2", query);
        Assert.Contains("status=Pending", query);
        Assert.Contains("due=Overdue", query);
        Assert.Contains("tagId=9", query);
        Assert.Contains("search=tap%20%26%20sink", query);
        Assert.Contains("pageSize=20", query);
        Assert.Contains("today=2026-06-15", query);
    }

    [Fact]
    public async Task Updating_a_task_sends_every_field_but_leaves_the_checklist_alone()
    {
        var api = new FakeTodoApi
        {
            Override = r => r.Method == HttpMethod.Put ? FakeTodoApi.JsonResponse(HttpStatusCode.OK, FakeTodoApi.TaskJson(1, "T")) : null,
        };

        await api.CreateClient().UpdateTaskAsync(1, new UpdateTaskPayload(2, "T", null, new DateOnly(2026, 11, 28), TodoPriority.High, TodoStatus.InProgress, new[] { 4 }));

        var body = api.Requests.Single(r => r.Method == HttpMethod.Put).Body!;
        Assert.Contains("\"sectionId\":2", body);
        Assert.Contains("\"dueDate\":\"2026-11-28\"", body);
        Assert.Contains("\"priority\":\"High\"", body);
        Assert.Contains("\"status\":\"InProgress\"", body);
        Assert.Contains("\"tagIds\":[4]", body);
        Assert.DoesNotContain("subTasks", body); // omitted = API leaves the checklist untouched
    }
}
