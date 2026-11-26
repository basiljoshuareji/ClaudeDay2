using System.ComponentModel.DataAnnotations;

namespace TodoApp.Api.Dtos;

/// <summary>A section with task counts. <c>PendingCount</c> is everything not completed (Pending + InProgress).</summary>
public record SectionDto(
    int Id,
    string Name,
    string Color,
    string Icon,
    int SortOrder,
    bool IsDefault,
    int TotalCount,
    int PendingCount,
    int InProgressCount,
    int CompletedCount);

public class SectionRequest
{
    [Required, StringLength(100, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [Required, RegularExpression("^#[0-9a-fA-F]{6}$", ErrorMessage = "Color must be a hex value like #3b82f6.")]
    public string Color { get; set; } = "#3b82f6";

    [Required, RegularExpression("^[a-z0-9-]{1,40}$", ErrorMessage = "Icon must be lowercase letters, digits or dashes.")]
    public string Icon { get; set; } = "folder";
}

public class ReorderRequest
{
    [Required, MinLength(1), MaxLength(500)]
    public List<int> OrderedIds { get; set; } = new();
}
