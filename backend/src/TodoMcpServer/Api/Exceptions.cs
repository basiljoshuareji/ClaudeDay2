using System.Net;

namespace TodoMcpServer.Api;

/// <summary>The API answered with an error status (validation, not found, ...).</summary>
public sealed class TodoApiException(HttpStatusCode statusCode, string code, string message, IReadOnlyDictionary<string, string[]>? fieldErrors = null)
    : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
    public string Code { get; } = code;
    public IReadOnlyDictionary<string, string[]> FieldErrors { get; } = fieldErrors ?? new Dictionary<string, string[]>();
}

/// <summary>The API could not be reached (down, DNS, TLS, timeout).</summary>
public sealed class TodoApiUnavailableException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>Signing in with the configured credentials failed.</summary>
public sealed class TodoAuthenticationException(string message) : Exception(message);

/// <summary>A name the user supplied (section, tag) does not exist. The message lists what does.</summary>
public class TodoLookupException(string message) : Exception(message);

public sealed class SectionNotFoundException(string requested, IReadOnlyList<string> available)
    : TodoLookupException($"Section '{requested}' not found. Available sections: {string.Join(", ", available)}.")
{
    public string Requested { get; } = requested;
    public IReadOnlyList<string> Available { get; } = available;
}

/// <summary>The tool input itself is invalid (unparseable date, unknown priority, ...).</summary>
public sealed class ToolInputException(string message) : Exception(message);
