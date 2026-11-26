using System.Security.Claims;
using TodoApp.Api.Exceptions;

namespace TodoApp.Api.Infrastructure;

public static class ClaimsPrincipalExtensions
{
    /// <summary>Reads the authenticated user's id from the JWT "sub" claim.</summary>
    public static int GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue("sub") ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(value, out var id)
            ? id
            : throw new UnauthorizedException("The access token does not identify a user.");
    }
}
