using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Authentication;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using TodoMcpServer.Configuration;

namespace TodoMcpServer.Api;

/// <summary>
/// Typed HTTP client for the Todo REST API.
///
/// Signs in with the configured account, keeps the JWT in memory (<see cref="TodoTokenStore"/>) and, when the API
/// answers 401, signs in again exactly once and retries the request before giving up.
///
/// TODO: storing the account password in the MCP client's config is a stop-gap. A revocable, scoped personal access
/// token issued by the Todo API would be a better replacement: it can be revoked without changing the password and
/// would never expose the real credentials.
/// </summary>
public sealed class TodoApiClient(
    HttpClient http,
    TodoApiOptions options,
    TodoTokenStore tokens,
    TimeProvider clock,
    ILogger<TodoApiClient> logger)
{
    /// <summary>Refresh the token slightly before it really expires.</summary>
    private static readonly TimeSpan ExpirySkew = TimeSpan.FromSeconds(60);

    private const string DefaultTagColor = "#64748b";

    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    // ------------------------------------------------------------------ sections and tags

    public async Task<IReadOnlyList<SectionInfo>> GetSectionsAsync(CancellationToken ct = default) =>
        await SendAsync<List<SectionInfo>>(() => new HttpRequestMessage(HttpMethod.Get, "api/sections"), ct);

    /// <summary>Finds a section by name (case-insensitive). The error lists the sections that do exist.</summary>
    public async Task<SectionInfo> ResolveSectionAsync(string? name, CancellationToken ct = default)
    {
        return FindSection(await GetSectionsAsync(ct), name);
    }

    /// <summary>Case-insensitive, whitespace-trimmed lookup in an already fetched section list.</summary>
    public static SectionInfo FindSection(IReadOnlyList<SectionInfo> sections, string? name)
    {
        var wanted = (name ?? string.Empty).Trim();
        return sections.FirstOrDefault(s => string.Equals(s.Name, wanted, StringComparison.OrdinalIgnoreCase))
               ?? throw new SectionNotFoundException(wanted, sections.Select(s => s.Name).ToList());
    }

    public async Task<IReadOnlyList<TagInfo>> GetTagsAsync(CancellationToken ct = default) =>
        await SendAsync<List<TagInfo>>(() => new HttpRequestMessage(HttpMethod.Get, "api/tags"), ct);

    /// <summary>
    /// Maps tag names to ids (case-insensitive). Unknown names are created when <paramref name="createMissing"/> is set,
    /// otherwise a <see cref="TodoLookupException"/> listing the existing tags is thrown.
    /// </summary>
    public async Task<(IReadOnlyList<int> Ids, IReadOnlyList<string> Created)> ResolveTagIdsAsync(
        IEnumerable<string>? names,
        bool createMissing,
        CancellationToken ct = default)
    {
        var wanted = (names ?? Enumerable.Empty<string>())
            .Select(n => n?.Trim() ?? string.Empty)
            .Where(n => n.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (wanted.Count == 0)
        {
            return (Array.Empty<int>(), Array.Empty<string>());
        }

        var existing = (await GetTagsAsync(ct)).ToList();
        var ids = new List<int>();
        var created = new List<string>();

        foreach (var name in wanted)
        {
            var match = existing.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
            if (match is null)
            {
                if (!createMissing)
                {
                    var available = existing.Count == 0 ? "none yet" : string.Join(", ", existing.Select(t => t.Name));
                    throw new TodoLookupException($"Tag '{name}' not found. Available tags: {available}.");
                }

                var body = new { name, color = DefaultTagColor };
                match = await SendAsync<TagInfo>(() => new HttpRequestMessage(HttpMethod.Post, "api/tags") { Content = JsonContent.Create(body, options: Json) }, ct);
                existing.Add(match);
                created.Add(match.Name);
            }

            ids.Add(match.Id);
        }

        return (ids, created);
    }

    // ------------------------------------------------------------------------------ tasks

    public async Task<PagedResult<TaskInfo>> ListTasksAsync(TaskListQuery query, CancellationToken ct = default)
    {
        var parts = new List<string>
        {
            "sortBy=DueDate",
            $"pageSize={Math.Clamp(query.PageSize, 1, 200)}",
            // Today/Overdue are relative to the user's own calendar date, not the server's.
            $"today={(query.Today ?? DateOnly.FromDateTime(clock.GetLocalNow().DateTime)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}",
        };
        if (query.SectionId is int sectionId) parts.Add($"sectionId={sectionId}");
        if (query.Status is TodoStatus status) parts.Add($"status={status}");
        if (query.Due is TodoDueFilter due) parts.Add($"due={due}");
        if (query.TagId is int tagId) parts.Add($"tagId={tagId}");
        if (!string.IsNullOrWhiteSpace(query.Search)) parts.Add($"search={Uri.EscapeDataString(query.Search.Trim())}");

        var url = "api/tasks?" + string.Join('&', parts);
        return await SendAsync<PagedResult<TaskInfo>>(() => new HttpRequestMessage(HttpMethod.Get, url), ct);
    }

    public async Task<TaskInfo> GetTaskAsync(int id, CancellationToken ct = default) =>
        await SendAsync<TaskInfo>(() => new HttpRequestMessage(HttpMethod.Get, $"api/tasks/{id}"), ct);

    public async Task<TaskInfo> CreateTaskAsync(CreateTaskPayload payload, CancellationToken ct = default) =>
        await SendAsync<TaskInfo>(() => new HttpRequestMessage(HttpMethod.Post, "api/tasks") { Content = JsonContent.Create(payload, options: Json) }, ct);

    public async Task<TaskInfo> UpdateTaskAsync(int id, UpdateTaskPayload payload, CancellationToken ct = default) =>
        await SendAsync<TaskInfo>(() => new HttpRequestMessage(HttpMethod.Put, $"api/tasks/{id}") { Content = JsonContent.Create(payload, options: Json) }, ct);

    public async Task<TaskInfo> SetCompletionAsync(int id, bool isCompleted, CancellationToken ct = default)
    {
        var body = new { isCompleted };
        return await SendAsync<TaskInfo>(() => new HttpRequestMessage(HttpMethod.Patch, $"api/tasks/{id}/completion") { Content = JsonContent.Create(body, options: Json) }, ct);
    }

    public async Task<SubTaskInfo> AddSubTaskAsync(int taskId, string title, CancellationToken ct = default)
    {
        var body = new { title };
        return await SendAsync<SubTaskInfo>(() => new HttpRequestMessage(HttpMethod.Post, $"api/tasks/{taskId}/subtasks") { Content = JsonContent.Create(body, options: Json) }, ct);
    }

    public async Task DeleteTaskAsync(int id, CancellationToken ct = default)
    {
        using var response = await SendAuthorizedAsync(() => new HttpRequestMessage(HttpMethod.Delete, $"api/tasks/{id}"), ct);
        await EnsureSuccessAsync(response, ct);
    }

    /// <summary>
    /// Duplicate guard: returns an <b>open</b> (not completed) task in the section whose title equals
    /// <paramref name="title"/> (case-insensitive, whitespace-normalised), or null.
    /// </summary>
    public async Task<TaskInfo?> FindOpenDuplicateAsync(int sectionId, string title, CancellationToken ct = default)
    {
        var wanted = NormalizeTitle(title);
        var search = title.Trim();
        var page = await ListTasksAsync(
            new TaskListQuery(SectionId: sectionId, Search: search.Length > 200 ? search[..200] : search, PageSize: 200),
            ct);
        return page.Items.FirstOrDefault(t => t.Status != TodoStatus.Completed && NormalizeTitle(t.Title) == wanted);
    }

    internal static string NormalizeTitle(string title) =>
        string.Join(' ', title.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToLowerInvariant();

    // ------------------------------------------------------------------ transport and auth

    private async Task<T> SendAsync<T>(Func<HttpRequestMessage> build, CancellationToken ct)
    {
        using var response = await SendAuthorizedAsync(build, ct);
        await EnsureSuccessAsync(response, ct);
        try
        {
            return await response.Content.ReadFromJsonAsync<T>(Json, ct)
                   ?? throw new TodoApiException(response.StatusCode, "empty_response", "The Todo API returned an empty response.");
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Unexpected response shape from the Todo API");
            throw new TodoApiException(response.StatusCode, "invalid_response", "The Todo API returned a response this tool could not understand (is TODO_API_URL pointing at the Todo API?).");
        }
    }

    /// <summary>Sends with the cached token; on 401 signs in again once and retries once.</summary>
    private async Task<HttpResponseMessage> SendAuthorizedAsync(Func<HttpRequestMessage> build, CancellationToken ct)
    {
        var token = await GetTokenAsync(rejectedToken: null, ct);
        var response = await SendOnceAsync(build, token, ct);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
        {
            return response;
        }

        response.Dispose();
        logger.LogInformation("The Todo API rejected the cached token; signing in again");
        token = await GetTokenAsync(rejectedToken: token, ct);
        return await SendOnceAsync(build, token, ct);
    }

    private async Task<HttpResponseMessage> SendOnceAsync(Func<HttpRequestMessage> build, string token, CancellationToken ct)
    {
        using var request = build();
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return await SendCoreAsync(request, ct);
    }

    private async Task<string> GetTokenAsync(string? rejectedToken, CancellationToken ct)
    {
        await tokens.Gate.WaitAsync(ct);
        try
        {
            // Reuse the cached token unless it has expired or the caller has just seen it rejected
            // (another concurrent call may already have refreshed it, in which case reuse that one).
            if (tokens.Token is { } cached && cached != rejectedToken && clock.GetUtcNow() < tokens.ExpiresAt - ExpirySkew)
            {
                return cached;
            }

            var auth = await LoginAsync(ct);
            tokens.Set(auth.AccessToken, new DateTimeOffset(DateTime.SpecifyKind(auth.ExpiresAt, DateTimeKind.Utc)));
            return auth.AccessToken;
        }
        finally
        {
            tokens.Gate.Release();
        }
    }

    private async Task<AuthResponse> LoginAsync(CancellationToken ct)
    {
        // The credentials only ever live in this request body: they are never logged.
        var body = new { email = options.Username, password = options.Password };
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/auth/login") { Content = JsonContent.Create(body, options: Json) };
        using var response = await SendCoreAsync(request, ct);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.BadRequest)
        {
            tokens.Clear();
            throw new TodoAuthenticationException(
                $"Signing in to the Todo API failed for '{options.Username}'. Check {TodoApiOptions.EnvUsername} (the account's email) and {TodoApiOptions.EnvPassword}.");
        }

        await EnsureSuccessAsync(response, ct);
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(Json, ct)
                   ?? throw new TodoApiException(response.StatusCode, "empty_response", "The login response was empty.");
        logger.LogInformation("Signed in to the Todo API");
        return auth;
    }

    private async Task<HttpResponseMessage> SendCoreAsync(HttpRequestMessage request, CancellationToken ct)
    {
        try
        {
            return await http.SendAsync(request, HttpCompletionOption.ResponseContentRead, ct);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new TodoApiUnavailableException($"The Todo API at {options.ApiUrl} did not respond within {options.TimeoutSeconds} seconds.");
        }
        catch (HttpRequestException ex)
        {
            var hint = HasCertificateProblem(ex)
                ? $" The HTTPS certificate is not trusted; for a local dev certificate you can set {TodoApiOptions.EnvAllowInsecureDevCert}=true."
                : string.Empty;
            throw new TodoApiUnavailableException($"Could not reach the Todo API at {options.ApiUrl}. Is it running? ({ex.Message}){hint}", ex);
        }
    }

    private static bool HasCertificateProblem(Exception ex)
    {
        for (var inner = ex.InnerException; inner is not null; inner = inner.InnerException)
        {
            if (inner is AuthenticationException) return true;
        }

        return false;
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        ApiErrorBody? body = null;
        try
        {
            body = await response.Content.ReadFromJsonAsync<ApiErrorBody>(Json, ct);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException or InvalidOperationException)
        {
            // Not the API's error shape (e.g. a proxy error page): fall through to the generic message.
        }

        var message = !string.IsNullOrWhiteSpace(body?.Message)
            ? body!.Message
            : $"The Todo API returned HTTP {(int)response.StatusCode}.";
        if (body?.Errors is { Count: > 0 } errors)
        {
            message += " " + string.Join("; ", errors.Select(e => $"{e.Key}: {string.Join(" ", e.Value)}"));
        }

        throw new TodoApiException(response.StatusCode, body?.Code ?? "error", message, body?.Errors);
    }
}
