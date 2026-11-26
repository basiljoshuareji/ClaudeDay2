using Microsoft.EntityFrameworkCore;
using TodoApp.Api.Dtos;
using TodoApp.Api.Exceptions;
using TodoApp.Tests.TestSupport;

namespace TodoApp.Tests.Services;

public class AuthServiceTests : IDisposable
{
    private readonly TestContext _t = new();

    public void Dispose() => _t.Dispose();

    private static RegisterRequest Register(string email = "ada@example.com", string password = "Passw0rd!", string? theme = null) =>
        new() { Email = email, Password = password, DisplayName = "  Ada  ", Theme = theme };

    [Fact]
    public async Task Register_creates_user_with_default_home_and_work_sections()
    {
        var result = await _t.Auth.RegisterAsync(Register());

        Assert.False(string.IsNullOrWhiteSpace(result.AccessToken));
        Assert.Equal("Ada", result.User.DisplayName);

        var sections = await _t.Sections.GetAllAsync(result.User.Id);
        Assert.Equal(new[] { "Home", "Work" }, sections.Select(s => s.Name).ToArray());
        Assert.All(sections, s => Assert.True(s.IsDefault));
    }

    [Fact]
    public async Task Register_never_stores_the_plain_password()
    {
        await _t.Auth.RegisterAsync(Register());

        var hash = await _t.Db.Users.Select(u => u.PasswordHash).SingleAsync();
        Assert.DoesNotContain("Passw0rd!", hash);
        Assert.StartsWith("$2", hash); // BCrypt
    }

    [Fact]
    public async Task Register_applies_requested_theme_and_falls_back_for_unknown_ones()
    {
        var ocean = await _t.Auth.RegisterAsync(Register("a@example.com", theme: "ocean"));
        var bogus = await _t.Auth.RegisterAsync(Register("b@example.com", theme: "neon-pink"));

        Assert.Equal("ocean", ocean.Preferences.Theme);
        Assert.Equal("light", bogus.Preferences.Theme);
    }

    [Fact]
    public async Task Register_rejects_duplicate_email_case_insensitively()
    {
        await _t.Auth.RegisterAsync(Register("ada@example.com"));

        var ex = await Assert.ThrowsAsync<ConflictException>(() => _t.Auth.RegisterAsync(Register("ADA@Example.com")));
        Assert.True(ex.Errors!.ContainsKey("email"));
    }

    [Fact]
    public async Task Login_succeeds_with_correct_credentials_regardless_of_email_case()
    {
        await _t.Auth.RegisterAsync(Register("ada@example.com"));

        var result = await _t.Auth.LoginAsync(new LoginRequest { Email = "Ada@Example.com", Password = "Passw0rd!" });

        Assert.Equal("ada@example.com", result.User.Email);
        Assert.True(result.ExpiresAt > _t.Clock.Now.UtcDateTime);
    }

    [Theory]
    [InlineData("ada@example.com", "wrong-password1")]
    [InlineData("nobody@example.com", "Passw0rd!")]
    public async Task Login_fails_with_a_generic_message_for_bad_credentials(string email, string password)
    {
        await _t.Auth.RegisterAsync(Register("ada@example.com"));

        var ex = await Assert.ThrowsAsync<UnauthorizedException>(
            () => _t.Auth.LoginAsync(new LoginRequest { Email = email, Password = password }));

        Assert.Equal("Invalid email or password.", ex.Message);
    }

    [Fact]
    public async Task GetSession_returns_user_and_preferences()
    {
        var auth = await _t.Auth.RegisterAsync(Register(theme: "forest"));

        var session = await _t.Auth.GetSessionAsync(auth.User.Id);

        Assert.Equal("ada@example.com", session.User.Email);
        Assert.Equal("forest", session.Preferences.Theme);
    }
}
