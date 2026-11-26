using System.ComponentModel.DataAnnotations;

namespace TodoApp.Api.Infrastructure;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    [Required]
    public string Issuer { get; set; } = "TodoApp";

    [Required]
    public string Audience { get; set; } = "TodoApp.Client";

    /// <summary>HMAC signing key (at least 32 characters). Provide via user-secrets or environment variables, never commit it.</summary>
    [Required, MinLength(32, ErrorMessage = "Jwt:Key must be at least 32 characters. Set it with user-secrets or the Jwt__Key environment variable.")]
    public string Key { get; set; } = string.Empty;

    [Range(1, 60 * 24 * 30)]
    public int ExpiryMinutes { get; set; } = 120;
}

public class SecurityOptions
{
    public const string SectionName = "Security";

    /// <summary>BCrypt cost factor. 11-12 is a good production value; tests lower it for speed.</summary>
    [Range(4, 31)]
    public int BcryptWorkFactor { get; set; } = 11;
}

public class TrashOptions
{
    public const string SectionName = "Trash";

    /// <summary>Soft-deleted tasks are kept this long (so "Undo" works) and then purged.</summary>
    public int RetentionHours { get; set; } = 24;

    public bool PurgeEnabled { get; set; } = true;

    public int IntervalMinutes { get; set; } = 60;
}
