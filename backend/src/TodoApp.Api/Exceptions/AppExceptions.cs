namespace TodoApp.Api.Exceptions;

/// <summary>Base class for expected, client-facing errors. Mapped to HTTP responses by the exception middleware.</summary>
public abstract class AppException : Exception
{
    protected AppException(string message, int statusCode, string code, string? field = null)
        : base(message)
    {
        StatusCode = statusCode;
        Code = code;
        if (field is not null)
        {
            Errors = new Dictionary<string, string[]> { [field] = new[] { message } };
        }
    }

    public int StatusCode { get; }
    public string Code { get; }
    public IDictionary<string, string[]>? Errors { get; }
}

public class NotFoundException(string entity) : AppException($"{entity} was not found.", StatusCodes.Status404NotFound, "not_found");

public class ConflictException(string message, string? field = null) : AppException(message, StatusCodes.Status409Conflict, "conflict", field);

public class BadRequestException(string message, string? field = null) : AppException(message, StatusCodes.Status400BadRequest, "bad_request", field);

public class UnauthorizedException(string message) : AppException(message, StatusCodes.Status401Unauthorized, "unauthorized");
