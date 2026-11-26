using System.Net;
using System.Text;
using System.Text.Json;
using TodoMcpServer.Api;
using TodoMcpServer.Configuration;

namespace TodoMcpServer.Tests.TestSupport;

public sealed record RecordedRequest(HttpMethod Method, string PathAndQuery, string? Authorization, string? Body)
{
    public string Path => PathAndQuery.Split('?')[0];
}

/// <summary>Controllable clock.</summary>
public sealed class FakeClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;
    public override DateTimeOffset GetUtcNow() => Now.ToUniversalTime();
    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
}

/// <summary>
/// A mocked HttpMessageHandler that behaves like a small in-memory Todo API (login, sections, tags, tasks) and records every
/// request. Tests can override <see cref="Override"/> to inject failures (401, 500, network errors) for specific calls.
/// </summary>
public sealed class FakeTodoApi : HttpMessageHandler
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public List<RecordedRequest> Requests { get; } = new();

    public List<object> Sections { get; } = new()
    {
        Section(1, "Home"), Section(2, "Work"), Section(3, "Fitness"),
    };

    public List<object> Tags { get; } = new();
    public List<Dictionary<string, object?>> Tasks { get; } = new();

    public int LoginCount => Requests.Count(r => r.Path == "/api/auth/login");
    public int TokenCounter { get; private set; }

    /// <summary>The token the fake API currently accepts. Set to something else to simulate a revoked/expired token.</summary>
    public string? AcceptedToken { get; set; }

    public bool LoginSucceeds { get; set; } = true;

    /// <summary>Return a response to take over a request; return null to fall through to the default behaviour.</summary>
    public Func<RecordedRequest, HttpResponseMessage?>? Override { get; set; }

    public static object Section(int id, string name) => new
    {
        id, name, color = "#3b82f6", icon = "home", sortOrder = id, isDefault = id <= 2,
        totalCount = 0, pendingCount = 0, inProgressCount = 0, completedCount = 0,
    };

    public static Dictionary<string, object?> TaskJson(
        int id, string title, int sectionId = 1, string status = "Pending", string? due = null, string priority = "Medium") => new()
    {
        ["id"] = id, ["sectionId"] = sectionId, ["title"] = title, ["description"] = null, ["dueDate"] = due,
        ["priority"] = priority, ["status"] = status, ["sortOrder"] = id, ["completedAt"] = null,
        ["createdAt"] = "2026-06-15T10:00:00Z", ["updatedAt"] = "2026-06-15T10:00:00Z",
        ["tags"] = Array.Empty<object>(), ["subTasks"] = Array.Empty<object>(),
    };

    public static HttpResponseMessage JsonResponse(HttpStatusCode status, object? body) => new(status)
    {
        Content = new StringContent(JsonSerializer.Serialize(body, Json), Encoding.UTF8, "application/json"),
    };

    public static HttpResponseMessage ApiError(HttpStatusCode status, string code, string message, object? errors = null) =>
        JsonResponse(status, new { status = (int)status, code, message, errors, traceId = "t" });

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        var recorded = new RecordedRequest(request.Method, request.RequestUri!.PathAndQuery, request.Headers.Authorization?.ToString(), body);
        Requests.Add(recorded);

        if (Override?.Invoke(recorded) is { } overridden)
        {
            return overridden;
        }

        return Default(recorded);
    }

    private HttpResponseMessage Default(RecordedRequest r)
    {
        if (r.Path == "/api/auth/login")
        {
            if (!LoginSucceeds) return ApiError(HttpStatusCode.Unauthorized, "unauthorized", "Invalid email or password.");
            TokenCounter++;
            AcceptedToken = $"token-{TokenCounter}";
            return JsonResponse(HttpStatusCode.OK, new
            {
                accessToken = AcceptedToken,
                expiresAt = DateTime.UtcNow.AddHours(2).ToString("o"),
                user = new { id = 1, email = "ada@example.com", displayName = "Ada" },
                preferences = new { theme = "light", sidebarCollapsed = false },
            });
        }

        if (r.Authorization != $"Bearer {AcceptedToken}")
        {
            return ApiError(HttpStatusCode.Unauthorized, "unauthorized", "Authentication is required or the token is invalid or expired.");
        }

        var key = $"{r.Method} {r.Path}";
        switch (key)
        {
            case "GET /api/sections": return JsonResponse(HttpStatusCode.OK, Sections);
            case "GET /api/tags": return JsonResponse(HttpStatusCode.OK, Tags);
            case "POST /api/tags":
            {
                var request = JsonSerializer.Deserialize<JsonElement>(r.Body!);
                var tag = new { id = Tags.Count + 1, name = request.GetProperty("name").GetString(), color = "#64748b", taskCount = 0 };
                Tags.Add(tag);
                return JsonResponse(HttpStatusCode.Created, tag);
            }
            case "GET /api/tasks":
                return JsonResponse(HttpStatusCode.OK, new { items = Tasks, page = 1, pageSize = 200, totalCount = Tasks.Count, totalPages = 1 });
            case "POST /api/tasks":
            {
                var request = JsonSerializer.Deserialize<JsonElement>(r.Body!);
                var task = TaskJson(
                    Tasks.Count + 100,
                    request.GetProperty("title").GetString()!,
                    request.GetProperty("sectionId").GetInt32(),
                    due: request.GetProperty("dueDate").ValueKind == JsonValueKind.Null ? null : request.GetProperty("dueDate").GetString(),
                    priority: request.GetProperty("priority").GetString()!);
                Tasks.Add(task);
                return JsonResponse(HttpStatusCode.Created, task);
            }
        }

        if (r.Method == HttpMethod.Get && r.Path.StartsWith("/api/tasks/"))
        {
            var id = int.Parse(r.Path.Split('/')[3]);
            var task = Tasks.FirstOrDefault(t => (int)t["id"]! == id);
            return task is null ? ApiError(HttpStatusCode.NotFound, "not_found", "Task was not found.") : JsonResponse(HttpStatusCode.OK, task);
        }

        if (r.Method == HttpMethod.Delete && r.Path.StartsWith("/api/tasks/"))
        {
            var id = int.Parse(r.Path.Split('/')[3]);
            Tasks.RemoveAll(t => (int)t["id"]! == id);
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }

        return ApiError(HttpStatusCode.NotFound, "not_found", $"No fake route for {key}.");
    }

    // ----------------------------------------------------------------------------- factories

    public static TodoApiOptions DefaultOptions() => new()
    {
        ApiUrl = "http://api.test",
        Username = "ada@example.com",
        Password = "Passw0rd1",
    };

    public TodoApiClient CreateClient(TimeProvider? clock = null, TodoApiOptions? options = null) =>
        new(
            new HttpClient(this) { BaseAddress = new Uri("http://api.test/") },
            options ?? DefaultOptions(),
            new TodoTokenStore(),
            clock ?? new FakeClock(new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero)),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<TodoApiClient>.Instance);
}
