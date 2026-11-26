using Microsoft.Extensions.Configuration;

namespace TodoMcpServer.Configuration;

/// <summary>
/// Connection settings for the Todo REST API.
/// Environment variables win; <c>appsettings.json</c> / <c>appsettings.local.json</c> (section "Todo") are the fallback
/// for local development.
/// </summary>
public sealed class TodoApiOptions
{
    public const string EnvApiUrl = "TODO_API_URL";
    public const string EnvUsername = "TODO_USERNAME";
    public const string EnvPassword = "TODO_PASSWORD";
    public const string EnvAllowInsecureDevCert = "TODO_ALLOW_INSECURE_DEV_CERT";
    public const string EnvTimeoutSeconds = "TODO_TIMEOUT_SECONDS";

    /// <summary>Base URL of the running Todo API, e.g. http://localhost:5080 (no trailing slash needed).</summary>
    public string ApiUrl { get; init; } = string.Empty;

    /// <summary>The Todo app account. The API signs in with the account's <b>email address</b>.</summary>
    public string Username { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;

    /// <summary>Accept an untrusted (self-signed) HTTPS certificate. Off by default; only honoured for localhost URLs.</summary>
    public bool AllowInsecureDevCert { get; init; }

    public int TimeoutSeconds { get; init; } = 30;

    /// <summary>Reads settings from environment variables first, then from configuration.</summary>
    public static TodoApiOptions Load(IConfiguration configuration, Func<string, string?>? environment = null)
    {
        environment ??= Environment.GetEnvironmentVariable;

        string Pick(string envName, string configKey) =>
            (Clean(environment(envName)) ?? Clean(configuration[$"Todo:{configKey}"]) ?? string.Empty);

        var insecure = Pick(EnvAllowInsecureDevCert, "AllowInsecureDevCert");
        var timeout = Pick(EnvTimeoutSeconds, "TimeoutSeconds");

        return new TodoApiOptions
        {
            ApiUrl = Pick(EnvApiUrl, "ApiUrl").TrimEnd('/'),
            Username = Pick(EnvUsername, "Username"),
            Password = Pick(EnvPassword, "Password"),
            AllowInsecureDevCert = insecure.Equals("true", StringComparison.OrdinalIgnoreCase) || insecure == "1",
            TimeoutSeconds = int.TryParse(timeout, out var seconds) ? seconds : 30,
        };
    }

    /// <summary>Returns human-readable problems; an empty list means the configuration is usable.</summary>
    public IReadOnlyList<string> Validate()
    {
        var problems = new List<string>();

        if (string.IsNullOrWhiteSpace(ApiUrl))
        {
            problems.Add($"{EnvApiUrl} is not set (base URL of the Todo API, e.g. http://localhost:5080).");
        }
        else if (!Uri.TryCreate(ApiUrl, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            problems.Add($"{EnvApiUrl} must be an absolute http:// or https:// URL.");
        }
        else if (AllowInsecureDevCert && !uri.IsLoopback)
        {
            problems.Add($"{EnvAllowInsecureDevCert} is only allowed when {EnvApiUrl} points at localhost.");
        }

        if (string.IsNullOrWhiteSpace(Username))
        {
            problems.Add($"{EnvUsername} is not set (the email address of your Todo account).");
        }

        if (string.IsNullOrEmpty(Password))
        {
            problems.Add($"{EnvPassword} is not set.");
        }

        if (TimeoutSeconds is < 1 or > 300)
        {
            problems.Add($"{EnvTimeoutSeconds} must be between 1 and 300.");
        }

        return problems;
    }

    /// <summary>Safe for logging: never includes the password.</summary>
    public override string ToString() =>
        $"ApiUrl={ApiUrl}, Username={Username}, AllowInsecureDevCert={AllowInsecureDevCert}, TimeoutSeconds={TimeoutSeconds}";

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
