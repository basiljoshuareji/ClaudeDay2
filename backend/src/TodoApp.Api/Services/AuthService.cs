using Microsoft.EntityFrameworkCore;
using TodoApp.Api.Data;
using TodoApp.Api.Domain;
using TodoApp.Api.Dtos;
using TodoApp.Api.Exceptions;
using TodoApp.Api.Mapping;

namespace TodoApp.Api.Services;

public class AuthService(
    AppDbContext db,
    IPasswordHasher hasher,
    ITokenService tokens,
    TimeProvider clock,
    ILogger<AuthService> logger) : IAuthService
{
    /// <summary>Sections created for every new account.</summary>
    public static readonly IReadOnlyList<(string Name, string Icon, string Color)> DefaultSections = new[]
    {
        ("Home", "home", "#3b82f6"),
        ("Work", "briefcase", "#8b5cf6")
    };

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var email = NormalizeEmail(request.Email);
        if (await db.Users.AnyAsync(u => u.Email == email, ct))
        {
            throw new ConflictException("An account with this email already exists.", "email");
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var user = new User
        {
            Email = email,
            DisplayName = request.DisplayName.Trim(),
            PasswordHash = hasher.Hash(request.Password),
            CreatedAt = now,
            Preference = new UserPreference
            {
                Theme = ThemeNames.IsValid(request.Theme) ? request.Theme! : ThemeNames.Default,
                UpdatedAt = now
            }
        };

        for (var i = 0; i < DefaultSections.Count; i++)
        {
            var (name, icon, color) = DefaultSections[i];
            user.Sections.Add(new Section { Name = name, Icon = icon, Color = color, SortOrder = i, IsDefault = true, CreatedAt = now });
        }

        db.Users.Add(user);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            if (await db.Users.AnyAsync(u => u.Email == email, ct))
            {
                // Lost a race with a concurrent registration for the same email.
                throw new ConflictException("An account with this email already exists.", "email");
            }

            throw;
        }

        logger.LogInformation("User {UserId} registered", user.Id);
        return CreateResponse(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var email = NormalizeEmail(request.Email);
        var user = await db.Users.Include(u => u.Preference).FirstOrDefaultAsync(u => u.Email == email, ct);

        // Always run one hash verification so response time does not reveal whether the email exists.
        var valid = hasher.Verify(request.Password, user?.PasswordHash ?? hasher.DummyHash);
        if (user is null || !valid)
        {
            logger.LogWarning("Failed login attempt");
            throw new UnauthorizedException("Invalid email or password.");
        }

        logger.LogInformation("User {UserId} logged in", user.Id);
        return CreateResponse(user);
    }

    public async Task<SessionDto> GetSessionAsync(int userId, CancellationToken ct = default)
    {
        var user = await db.Users.Include(u => u.Preference).FirstOrDefaultAsync(u => u.Id == userId, ct)
                   ?? throw new UnauthorizedException("The account no longer exists.");
        return new SessionDto(user.ToDto(), (user.Preference ?? new UserPreference()).ToDto());
    }

    private AuthResponse CreateResponse(User user)
    {
        var (token, expiresAt) = tokens.CreateToken(user);
        return new AuthResponse(token, expiresAt, user.ToDto(), (user.Preference ?? new UserPreference()).ToDto());
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
