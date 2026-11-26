using Microsoft.EntityFrameworkCore;
using TodoApp.Api.Dtos;
using TodoApp.Api.Exceptions;

namespace TodoApp.Api.Middleware;

/// <summary>
/// Global exception handler. Converts every failure into the same <see cref="ApiError"/> JSON shape
/// and logs unexpected errors with their stack trace (never leaking it to the client).
/// </summary>
public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Client went away; nothing to send.
            context.Response.StatusCode = 499;
        }
        catch (Exception ex) when (!context.Response.HasStarted)
        {
            await HandleAsync(context, ex);
        }
    }

    private async Task HandleAsync(HttpContext context, Exception ex)
    {
        switch (ex)
        {
            case AppException app:
                logger.LogInformation("Request failed with {Code}: {Message}", app.Code, app.Message);
                await ApiErrorWriter.WriteAsync(context, app.StatusCode, app.Code, app.Message, app.Errors);
                break;

            case BadHttpRequestException bad:
                logger.LogInformation("Bad request: {Message}", bad.Message);
                await ApiErrorWriter.WriteAsync(context, bad.StatusCode, "bad_request", "The request could not be processed.", null);
                break;

            case DbUpdateConcurrencyException:
                logger.LogWarning(ex, "Concurrency conflict");
                await ApiErrorWriter.WriteAsync(context, StatusCodes.Status409Conflict, "conflict",
                    "The item was changed by someone else. Reload and try again.", null);
                break;

            default:
                logger.LogError(ex, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);
                await ApiErrorWriter.WriteAsync(context, StatusCodes.Status500InternalServerError, "server_error",
                    "An unexpected error occurred. Please try again later.", null);
                break;
        }
    }
}

public static class ApiErrorWriter
{
    public static Task WriteAsync(
        HttpContext context,
        int status,
        string code,
        string message,
        IDictionary<string, string[]>? errors)
    {
        context.Response.Clear();
        context.Response.StatusCode = status;
        var body = new ApiError(status, code, message, errors, context.TraceIdentifier);
        return context.Response.WriteAsJsonAsync(body, context.RequestAborted);
    }
}
