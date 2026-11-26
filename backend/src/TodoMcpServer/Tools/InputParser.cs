using System.Globalization;
using System.Text.RegularExpressions;
using TodoMcpServer.Api;

namespace TodoMcpServer.Tools;

/// <summary>
/// Lenient, well-explained parsing of the free-form values Claude passes to tools.
/// Errors are <see cref="ToolInputException"/>s whose messages tell the model how to correct itself.
/// </summary>
public static partial class InputParser
{
    [GeneratedRegex(@"^(\d{4}-\d{2}-\d{2})(?:[T ](.*))?$")]
    private static partial Regex IsoDateStart();

    /// <summary>
    /// Accepts an ISO 8601 date (2026-11-28) or date-time (2026-11-28T17:00:00, optional offset or Z) and returns the calendar
    /// date <i>as written</i> (the API stores due dates without a time). Relative words are rejected with guidance.
    /// </summary>
    public static DateOnly ParseDueDate(string value, DateOnly today)
    {
        var text = value.Trim();
        var match = IsoDateStart().Match(text);
        if (match.Success
            && DateOnly.TryParseExact(match.Groups[1].Value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            && (!match.Groups[2].Success || DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out _)))
        {
            return date;
        }

        throw new ToolInputException(
            $"Could not understand the date '{value}'. Pass an absolute ISO 8601 date such as {today:yyyy-MM-dd} " +
            $"(or a date-time such as {today:yyyy-MM-dd}T17:00:00). Today is {today.ToString("dddd yyyy-MM-dd", CultureInfo.InvariantCulture)}; " +
            "convert relative dates like 'Saturday' or 'tomorrow' to an absolute date first.");
    }

    public static TodoPriority ParsePriority(string value) =>
        Normalize(value) switch
        {
            "low" => TodoPriority.Low,
            "medium" => TodoPriority.Medium,
            "high" => TodoPriority.High,
            _ => throw new ToolInputException($"Unknown priority '{value}'. Use Low, Medium or High."),
        };

    public static TodoStatus ParseStatus(string value) =>
        Normalize(value) switch
        {
            "pending" or "todo" or "open" or "notstarted" => TodoStatus.Pending,
            "inprogress" or "started" or "doing" => TodoStatus.InProgress,
            "completed" or "complete" or "done" => TodoStatus.Completed,
            _ => throw new ToolInputException($"Unknown status '{value}'. Use Pending, InProgress or Completed."),
        };

    public static TodoDueFilter ParseDueFilter(string value) =>
        Normalize(value) switch
        {
            "today" => TodoDueFilter.Today,
            "upcoming" => TodoDueFilter.Upcoming,
            "overdue" => TodoDueFilter.Overdue,
            "none" or "nodate" or "undated" => TodoDueFilter.None,
            _ => throw new ToolInputException($"Unknown due filter '{value}'. Use today, upcoming, overdue or none."),
        };

    /// <summary>
    /// Status filter for list_tasks: "open" (default; Pending + InProgress), "all", or a single status.
    /// Returns the API status filter (null = none) and whether completed tasks must be dropped client-side.
    /// </summary>
    public static (TodoStatus? Api, bool OpenOnly) ParseListStatus(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return (null, true);
        return Normalize(value) switch
        {
            "open" or "active" or "notdone" or "incomplete" => (null, true),
            "all" or "any" => (null, false),
            _ => (ParseStatus(value), false),
        };
    }

    private static string Normalize(string value) =>
        new string(value.Where(c => c is not (' ' or '_' or '-')).ToArray()).ToLowerInvariant();
}
