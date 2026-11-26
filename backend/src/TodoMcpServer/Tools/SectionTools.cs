using System.ComponentModel;
using System.Globalization;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using TodoMcpServer.Api;

namespace TodoMcpServer.Tools;

[McpServerToolType]
public sealed class SectionTools(TodoApiClient api, TimeProvider clock, ILogger<SectionTools> logger)
{
    [McpServerTool(Name = "list_sections", ReadOnly = true, OpenWorld = false)]
    [Description(
        "List the user's task sections (lists) such as Home and Work, plus any custom ones, with task counts " +
        "(total, open = not completed, in progress, done). Also returns today's date. Call this to discover valid " +
        "section names before adding or moving tasks. Section names are what the other tools take.")]
    public Task<string> ListSections(CancellationToken cancellationToken = default) =>
        ToolSupport.RunAsync("list_sections", logger, async () =>
        {
            var sections = await api.GetSectionsAsync(cancellationToken);
            return ToolSupport.ToJson(new
            {
                today = Describe(ToolSupport.Today(clock)),
                sections = sections.Select(s => new
                {
                    s.Id,
                    Name = ToolSupport.Clean(s.Name, 100),
                    Total = s.TotalCount,
                    Open = s.PendingCount,
                    InProgress = s.InProgressCount,
                    Done = s.CompletedCount,
                    IsDefault = s.IsDefault,
                }),
            });
        });

    [McpServerTool(Name = "get_current_date", ReadOnly = true, OpenWorld = false)]
    [Description(
        "Returns today's date and weekday on the user's computer. Call this BEFORE add_task or update_task whenever the user " +
        "gives a relative date such as 'Saturday', 'tomorrow' or 'next week', so you can convert it to an absolute ISO 8601 date.")]
    public Task<string> GetCurrentDate() =>
        ToolSupport.RunAsync("get_current_date", logger, () => Task.FromResult(
            $"Today is {Describe(ToolSupport.Today(clock))} (local time zone: {TimeZoneInfo.Local.DisplayName}). " +
            "Use absolute ISO 8601 dates (yyyy-MM-dd) when calling the other tools."));

    private static string Describe(DateOnly date) => date.ToString("dddd yyyy-MM-dd", CultureInfo.InvariantCulture);
}
