using System.ComponentModel.DataAnnotations;

namespace TodoApp.Api.Dtos;

public class RegisterRequest
{
    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required, MinLength(8), MaxLength(72)]
    [RegularExpression(@"^(?=.*[A-Za-z])(?=.*\d).+$", ErrorMessage = "Password must contain at least one letter and one number.")]
    public string Password { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 1)]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Optional initial theme (e.g. the one the visitor already picked before signing up).</summary>
    [MaxLength(20)]
    public string? Theme { get; set; }
}

public class LoginRequest
{
    [Required, EmailAddress, MaxLength(256)]
    public string Email { get; set; } = string.Empty;

    [Required, MaxLength(72)]
    public string Password { get; set; } = string.Empty;
}

public record UserDto(int Id, string Email, string DisplayName);

public record PreferenceDto(string Theme, bool SidebarCollapsed);

/// <summary>Current user and their preferences (returned by GET /api/auth/me).</summary>
public record SessionDto(UserDto User, PreferenceDto Preferences);

public record AuthResponse(string AccessToken, DateTime ExpiresAt, UserDto User, PreferenceDto Preferences);

public class UpdatePreferencesRequest
{
    [Required, MaxLength(20)]
    public string Theme { get; set; } = string.Empty;

    public bool SidebarCollapsed { get; set; }
}
