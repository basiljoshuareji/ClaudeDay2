using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TodoApp.Api.Data;
using TodoApp.Api.Domain;
using TodoApp.Api.Dtos;
using TodoApp.Api.Infrastructure;
using TodoApp.Api.Services;

namespace TodoApp.Tests.TestSupport;

/// <summary>A controllable clock so tests can reason about "today".</summary>
public class FakeClock(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;
    public override DateTimeOffset GetUtcNow() => Now;
}

/// <summary>
/// Real SQLite database in memory with the real EF migrations applied, plus all services wired up.
/// Each test gets its own isolated instance.
/// </summary>
public sealed class TestContext : IDisposable
{
    private readonly SqliteConnection _connection;

    public TestContext()
    {
        Clock = new FakeClock(new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero));
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        Db = NewContext();
        Db.Database.Migrate();

        var hasher = new BCryptPasswordHasher(Options.Create(new SecurityOptions { BcryptWorkFactor = 4 }));
        var tokens = new TokenService(
            Options.Create(new JwtOptions { Key = "unit-test-signing-key-0123456789abcdef", ExpiryMinutes = 30 }),
            Clock);

        Auth = new AuthService(Db, hasher, tokens, Clock, NullLogger<AuthService>.Instance);
        Sections = new SectionService(Db, Clock, NullLogger<SectionService>.Instance);
        Tasks = new TaskService(Db, Clock, NullLogger<TaskService>.Instance);
        SubTasks = new SubTaskService(Db, Clock);
        Tags = new TagService(Db);
        Preferences = new PreferenceService(Db, Clock);
    }

    public FakeClock Clock { get; }
    public AppDbContext Db { get; }
    public AuthService Auth { get; }
    public SectionService Sections { get; }
    public TaskService Tasks { get; }
    public SubTaskService SubTasks { get; }
    public TagService Tags { get; }
    public PreferenceService Preferences { get; }

    public DateOnly Today => DateOnly.FromDateTime(Clock.Now.UtcDateTime);

    private AppDbContext NewContext() => new(
        new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .ConfigureWarnings(w => w.Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning))
            .Options);

    /// <summary>Registers a user and returns their id plus their default section ids.</summary>
    public async Task<(int UserId, int HomeId, int WorkId)> CreateUserAsync(string email = "ada@example.com")
    {
        var auth = await Auth.RegisterAsync(new RegisterRequest
        {
            Email = email,
            Password = "Passw0rd!",
            DisplayName = "Test User"
        });
        var sections = await Sections.GetAllAsync(auth.User.Id);
        return (auth.User.Id, sections.Single(s => s.Name == "Home").Id, sections.Single(s => s.Name == "Work").Id);
    }

    public Task<TaskDto> AddTaskAsync(
        int userId,
        int sectionId,
        string title,
        TodoPriority priority = TodoPriority.Medium,
        DateOnly? due = null,
        TodoStatus status = TodoStatus.Pending,
        string? description = null,
        List<int>? tagIds = null) =>
        Tasks.CreateAsync(userId, new CreateTaskRequest
        {
            SectionId = sectionId,
            Title = title,
            Description = description,
            Priority = priority,
            DueDate = due,
            Status = status,
            TagIds = tagIds ?? new List<int>()
        });

    /// <summary>Drops cached entities so assertions read what is really in the database.</summary>
    public void ClearTracking() => Db.ChangeTracker.Clear();

    public void Dispose()
    {
        Db.Dispose();
        _connection.Dispose();
    }
}
