using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using TodoApp.Api.Domain;

namespace TodoApp.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<UserPreference> Preferences => Set<UserPreference>();
    public DbSet<Section> Sections => Set<Section>();
    public DbSet<TodoTask> Tasks => Set<TodoTask>();
    public DbSet<SubTask> SubTasks => Set<SubTask>();
    public DbSet<Tag> Tags => Set<Tag>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // SQLite returns DateTime with Kind=Unspecified. Force UTC so JSON always carries a "Z".
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.Property(x => x.Email).HasMaxLength(256).IsRequired();
            e.Property(x => x.DisplayName).HasMaxLength(100).IsRequired();
            e.Property(x => x.PasswordHash).HasMaxLength(100).IsRequired();
            e.HasIndex(x => x.Email).IsUnique();

            e.HasOne(x => x.Preference)
                .WithOne(p => p.User)
                .HasForeignKey<UserPreference>(p => p.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<UserPreference>(e =>
        {
            e.Property(x => x.Theme).HasMaxLength(20).IsRequired();
        });

        modelBuilder.Entity<Section>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.Property(x => x.Color).HasMaxLength(9).IsRequired();
            e.Property(x => x.Icon).HasMaxLength(40).IsRequired();
            e.HasIndex(x => new { x.UserId, x.SortOrder });

            e.HasOne(x => x.User)
                .WithMany(u => u.Sections)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Tag>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(50).IsRequired();
            e.Property(x => x.Color).HasMaxLength(9).IsRequired();
            e.HasIndex(x => new { x.UserId, x.Name }).IsUnique();

            // Restrict (not cascade) avoids "multiple cascade paths" errors on SQL Server.
            e.HasOne(x => x.User)
                .WithMany(u => u.Tags)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TodoTask>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.Description).HasMaxLength(4000);
            e.HasIndex(x => new { x.UserId, x.SectionId, x.SortOrder });
            e.HasIndex(x => new { x.UserId, x.Status });
            e.HasIndex(x => new { x.UserId, x.DueDate });

            e.HasOne(x => x.Section)
                .WithMany(s => s.Tasks)
                .HasForeignKey(x => x.SectionId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.User)
                .WithMany(u => u.Tasks)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasMany(x => x.Tags)
                .WithMany(t => t.Tasks)
                .UsingEntity(j => j.ToTable("TaskTags"));

            // Soft delete: hide deleted tasks everywhere unless IgnoreQueryFilters() is used.
            e.HasQueryFilter(x => x.DeletedAt == null);
        });

        modelBuilder.Entity<SubTask>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.HasOne(x => x.TodoTask)
                .WithMany(t => t.SubTasks)
                .HasForeignKey(x => x.TodoTaskId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasQueryFilter(x => x.TodoTask.DeletedAt == null);
        });
    }

    private sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
        v => v.Kind == DateTimeKind.Utc ? v : v.ToUniversalTime(),
        v => DateTime.SpecifyKind(v, DateTimeKind.Utc));
}
