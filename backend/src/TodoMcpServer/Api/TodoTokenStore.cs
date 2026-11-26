namespace TodoMcpServer.Api;

/// <summary>
/// In-memory JWT cache shared by every request (registered as a singleton, because the typed
/// <see cref="TodoApiClient"/> itself is transient). Never written to disk or logs.
/// </summary>
public sealed class TodoTokenStore
{
    public string? Token { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>Serialises logins so concurrent tool calls don't each sign in.</summary>
    public SemaphoreSlim Gate { get; } = new(1, 1);

    public void Set(string token, DateTimeOffset expiresAt)
    {
        Token = token;
        ExpiresAt = expiresAt;
    }

    public void Clear()
    {
        Token = null;
        ExpiresAt = default;
    }
}
