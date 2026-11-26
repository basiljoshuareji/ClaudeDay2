# Todo MCP server

Use your Todo app from **Claude Desktop** (or any MCP client). Tell Claude *"add 'fix the leaking tap' to my Home list, due
Saturday, high priority"* and the task appears in the app.

```
Claude Desktop  <-- stdio / MCP -->  TodoMcpServer  <-- HTTP + JWT -->  Todo API  -->  database
```

The server is a **thin adapter**. It never touches the database; every read and write goes through the existing REST API,
so validation, ownership checks and business rules stay in one place (the API). It runs as a child process of Claude
Desktop and talks MCP over stdin/stdout.

## Project layout

```
backend/src/TodoMcpServer/
├── Program.cs                  # host, DI, stdio transport, stderr logging, fail-fast config check
├── appsettings.json            # fallback settings (empty by default); appsettings.local.json is git-ignored
├── Configuration/
│   └── TodoApiOptions.cs       # env vars -> options + validation
├── Api/
│   ├── TodoApiClient.cs        # typed HttpClient: login, cached JWT, re-login on 401, section/tag/duplicate helpers
│   ├── TodoTokenStore.cs       # in-memory token cache (singleton)
│   ├── Models.cs               # wire models mirroring the API's JSON
│   └── Exceptions.cs
└── Tools/
    ├── SectionTools.cs         # list_sections, get_current_date
    ├── TaskTools.cs            # add_task, list_tasks, get_task, complete_task, update_task, add_subtask, delete_task
    ├── InputParser.cs          # lenient parsing of dates, priorities, statuses
    └── ToolSupport.cs          # error mapping, output formatting, sanitising of untrusted text
backend/tests/TodoMcpServer.Tests/   # 78 xUnit tests (mocked HttpMessageHandler, no network)
```

## Tools

Tools take human-friendly inputs: sections and tags are passed by **name** (case-insensitive) and the server resolves them to ids.

| Tool | What it does | Example prompt |
|---|---|---|
| `list_sections` | Sections with task counts, plus today's date | "What lists do I have?" |
| `get_current_date` | Today's date and weekday (so Claude can convert "Saturday") | *(called automatically)* |
| `add_task` | Create a task: `title`, `section` (default Home), `description`, `dueDate`, `priority`, `tags`, `subtasks`, `allowDuplicate` | "Add 'fix the leaking tap' to Home, due Saturday, high priority" |
| `list_tasks` | Filter by `section`, `status` (default: open), `due` (today/upcoming/overdue/none), `tag`, `search`, `limit` | "What's overdue?" · "Show my open Work tasks" |
| `get_task` | Full details of one task including checklist | "Show me task 12" |
| `complete_task` | Mark done, or reopen with `completed=false` | "Mark task 12 as done" |
| `update_task` | Change title, notes, due date (or `none`), priority, status, tags, or move section | "Move task 12 to Work and make it low priority" |
| `add_subtask` | Add a checklist item | "Add 'buy a washer' to the tap task" |
| `delete_task` | Delete a task. Needs `confirm=true`, and the description tells Claude to ask you first | "Delete task 12" |

Behaviour worth knowing:

* **Dates must be absolute ISO 8601** (`2026-11-28` or `2026-11-28T17:00:00`). Claude converts "Saturday"/"tomorrow" itself, using
  `get_current_date` to learn today's date. The result always echoes the weekday ("due Sat 2026-11-28") so a wrong conversion
  is easy to spot. Date-times are reduced to their calendar date because the app stores due dates without a time.
* **Duplicate guard:** `add_task` will not create a second *open* task with the same title in the same section. It reports the
  existing one instead; pass `allowDuplicate=true` to override.
* **Helpful errors:** `Section 'Gym' not found. Available sections: Home, Work, Fitness.` Claude can self-correct from these.
* **Untrusted data:** task text is user-controlled. Results are JSON (so text is clearly delimited), control characters are
  stripped, lengths are capped, a notice reminds the model that task text is data, and no tool ever acts on instructions found in a
  task. Destructive actions need an explicit `confirm=true`.
* **Failures never crash the process:** API down, timeouts, validation and auth errors come back as MCP error results.

## Configuration

Environment variables win; `appsettings.json` / `appsettings.local.json` (section `Todo`) are the fallback for local development.

| Variable | Required | Meaning |
|---|---|---|
| `TODO_API_URL` | yes | Base URL of the running Todo API, e.g. `http://localhost:5080` |
| `TODO_USERNAME` | yes | Your Todo app account **email** (the API signs in with the email) |
| `TODO_PASSWORD` | yes | Its password |
| `TODO_ALLOW_INSECURE_DEV_CERT` | no | `true` accepts a self-signed HTTPS certificate. Off by default; refused unless the URL is `localhost` |
| `TODO_TIMEOUT_SECONDS` | no | Per-request timeout, 1-300, default 30 |

If a required value is missing the process prints what to set to **stderr** and exits with code 1 (Claude Desktop shows the
server as failed and the message appears in its MCP log). Credentials and tokens are never logged.

For local development you can put values in `appsettings.local.json` next to `appsettings.json` (git-ignored):

```json
{ "Todo": { "ApiUrl": "http://localhost:5080", "Username": "you@example.com", "Password": "..." } }
```

> **Security note.** Storing your password in the client's config is a stop-gap (see the `TODO` in `TodoApiClient`). A revocable,
> scoped personal access token issued by the Todo API would be better: it could be revoked without changing your password. Until
> then, keep the config file private, and consider a separate Todo account for experiments.

## Run, test, publish

Start the Todo API first (the tools call it):

```powershell
cd backend
dotnet run --project src/TodoApp.Api            # http://localhost:5080
cd ../frontend; npm start                       # optional: the Angular UI on http://localhost:4200
```

Run the MCP server directly (it waits for MCP messages on stdin, so you normally let a client launch it):

```powershell
$env:TODO_API_URL = "http://localhost:5080"
$env:TODO_USERNAME = "you@example.com"
$env:TODO_PASSWORD = "your-password"
dotnet run --project backend/src/TodoMcpServer
```

Tests (78, no network or database needed):

```powershell
dotnet test backend/tests/TodoMcpServer.Tests
```

Publish a fast-starting build (recommended for Claude Desktop):

```powershell
dotnet publish backend/src/TodoMcpServer -c Release
# -> backend/src/TodoMcpServer/bin/Release/net8.0/publish/   (TodoMcpServer.dll and TodoMcpServer.exe)
```

### MCP Inspector

With the variables above set in the same PowerShell window:

```powershell
npx @modelcontextprotocol/inspector dotnet run --project backend/src/TodoMcpServer
```

Open the URL it prints, click **Connect**, then **Tools -> List tools** and try `list_sections`.
(`dotnet run` prints nothing to stdout when the build is clean, so it is safe here. For a build with warnings, or for the fastest
start, point the Inspector at the published dll: `npx @modelcontextprotocol/inspector dotnet "<path>/publish/TodoMcpServer.dll"`.)

## Connect Claude Desktop

1. Publish (above).
2. Claude Desktop -> **Settings -> Developer -> Edit Config**. This opens the right `claude_desktop_config.json` for your install:
   * Microsoft Store / MSIX install: `%LOCALAPPDATA%\Packages\Claude_pzs8sxrjxfjjc\LocalCache\Roaming\Claude\claude_desktop_config.json`
   * Standard installer: `%APPDATA%\Claude\claude_desktop_config.json`
3. Add the `mcpServers` entry (merge it if the file already has other servers).
4. **Fully quit and restart Claude Desktop** (close it from the system tray, not just the window). Config is read only at startup.
5. Make sure the Todo API is running, then ask Claude to list your sections.

**Published dll** (recommended):

```json
{
  "mcpServers": {
    "todo": {
      "command": "dotnet",
      "args": ["C:/Users/BasilJoshua/Desktop/Claude Day 2/backend/src/TodoMcpServer/bin/Release/net8.0/publish/TodoMcpServer.dll"],
      "env": {
        "TODO_API_URL": "http://localhost:5080",
        "TODO_USERNAME": "you@example.com",
        "TODO_PASSWORD": "your-password"
      }
    }
  }
}
```

**Published exe** (same thing, no `dotnet` in the command):

```json
{
  "mcpServers": {
    "todo": {
      "command": "C:/Users/BasilJoshua/Desktop/Claude Day 2/backend/src/TodoMcpServer/bin/Release/net8.0/publish/TodoMcpServer.exe",
      "args": [],
      "env": {
        "TODO_API_URL": "http://localhost:5080",
        "TODO_USERNAME": "you@example.com",
        "TODO_PASSWORD": "your-password"
      }
    }
  }
}
```

**`dotnet run`** (slower start, builds on first launch; handy while developing):

```json
{
  "mcpServers": {
    "todo": {
      "command": "dotnet",
      "args": ["run", "--no-launch-profile", "--project", "C:/Users/BasilJoshua/Desktop/Claude Day 2/backend/src/TodoMcpServer"],
      "env": {
        "TODO_API_URL": "http://localhost:5080",
        "TODO_USERNAME": "you@example.com",
        "TODO_PASSWORD": "your-password"
      }
    }
  }
}
```

Replace `you@example.com` / `your-password` with your Todo account. Use forward slashes (or doubled backslashes) in JSON paths.
If you later move the repo or re-publish elsewhere, update the path.

## Manual test checklist

Prerequisites: the API is running, the Angular UI is open at <http://localhost:4200> and you are signed in as the same account
you put in the config. Use the Inspector first, then Claude Desktop.

1. **List sections.** Ask *"What lists do I have?"* -> Home and Work (and any custom ones) with counts that match the UI sidebar.
2. **Add to Home.** *"Add 'fix the leaking tap' to my Home list, due Saturday, high priority."* -> Claude calls `get_current_date`, then
   `add_task`. The reply shows the task id and the due weekday. In the UI: the task is in **Home**, red/high, with the right date.
3. **Duplicate guard.** Repeat the same request -> nothing new is created; Claude reports the existing task.
4. **Unknown section.** *"Add 'run 5k' to Gym."* -> Claude is told which sections exist and asks or retries with a valid one.
5. **Read.** *"What's on my plate?"*, *"What's overdue?"*, *"Show task <id>."*
6. **Complete.** *"Mark the leaking tap task as done."* -> in the UI it moves to done and the section progress bar updates. Then *"Reopen it."*
7. **Update.** *"Move it to Work, make it low priority and push the due date to Monday."* -> the UI reflects all three changes.
8. **Checklist.** *"Add 'buy a washer' to that task."* -> the checklist chip in the UI shows 0/1.
9. **Delete needs confirmation.** *"Delete that task."* -> Claude must ask you first. Say yes -> it is gone from the UI.
10. **Failure handling.** Stop the API and ask for your lists -> a clear "could not reach the Todo API" message, no crash. Start it again and retry.

## Troubleshooting

**The server doesn't show up in Claude Desktop**
* Fully quit Claude Desktop (system tray -> Quit) and start it again; the config is only read at startup.
* Validate the JSON (a trailing comma breaks the whole file). Check the path points at a file that exists
  (`TodoMcpServer.dll` in the `publish` folder) and that `dotnet --list-runtimes` works in a normal terminal.
* Look at the logs (below).

**Logs.** Claude Desktop writes one log per server plus a general one, in a `logs` folder next to `claude_desktop_config.json`:
`mcp-server-todo.log` (everything this server writes to stderr, including the "invalid configuration" message) and `mcp.log`.
Standard install: `%APPDATA%\Claude\logs`; Store/MSIX install: `%LOCALAPPDATA%\Packages\Claude_pzs8sxrjxfjjc\LocalCache\Roaming\Claude\logs`.
On macOS: `~/Library/Logs/Claude`.

**"Server disconnected" / invalid JSON errors (stdout pollution).** stdout is the protocol channel; anything else written there
corrupts it. This server logs to stderr only and redirects `Console.Out` to stderr as a safety net. If you add code, never use
`Console.WriteLine`. If you launch via `dotnet run`, build *warnings* print to stdout and can break the handshake, so prefer the
published build.

**Certificate errors.** Plain `http://localhost:5080` needs no certificate. For `https://localhost:...` with a self-signed dev
certificate set `TODO_ALLOW_INSECURE_DEV_CERT=true` (honoured only for localhost), or trust the certificate with
`dotnet dev-certs https --trust`.

**401 / "Signing in failed".** `TODO_USERNAME` must be the account's **email**, and the password must be right. Expired or revoked
tokens are handled automatically: the server signs in again once and retries. A persistent 401 after that means the credentials
are wrong or the account was removed.

**"Could not reach the Todo API".** Start the API (`dotnet run --project backend/src/TodoApp.Api`) and check `TODO_API_URL`
(port 5080 by default). Open `<TODO_API_URL>/health`; it should say `Healthy`.

**"Section ... not found" / tasks are in the wrong place.** Section names are matched case-insensitively against your account's
sections; ask Claude to run `list_sections`. Make sure the config's account is the same one you are signed in to in the web app.
