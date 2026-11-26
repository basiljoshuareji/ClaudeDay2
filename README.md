# Todo: ASP.NET Core + Angular

A multi-user to-do application. Tasks live in **sections** (Home and Work are created for every new account),
carry sub-task checklists and tags, and can be searched, filtered, sorted, bulk-edited and re-ordered by
drag and drop. Five runtime-switchable **themes** are saved per user.

| | |
|---|---|
| **Backend** | C#, ASP.NET Core Web API (`net8.0`), EF Core 8, SQLite (SQL Server by config), JWT auth, Serilog, Swagger |
| **Frontend** | Angular 22 (standalone components, signals, zoneless), Router, Reactive Forms, SCSS, Angular CDK drag-drop |
| **MCP server** | `TodoMcpServer` (C# console, stdio): use the app from Claude Desktop. See [its README](backend/src/TodoMcpServer/README.md) |
| **Tests** | 69 API tests (xUnit: service layer + HTTP integration), 78 MCP server tests (xUnit), 73 frontend tests (Vitest) |

## Project structure

```
.
├── README.md
├── backend/
│   ├── TodoApp.sln
│   ├── dotnet-tools.json                      # pins dotnet-ef 8.0.34
│   ├── src/TodoApp.Api/
│   │   ├── Program.cs                         # composition root: DI, JWT, CORS, Swagger, pipeline
│   │   ├── appsettings*.json
│   │   ├── Controllers/                       # thin HTTP layer (Auth, Sections, Tasks, SubTasks, Tags, Preferences)
│   │   ├── Services/                          # business rules (one service per aggregate) + interfaces
│   │   ├── Data/                              # AppDbContext + Migrations/
│   │   ├── Domain/                            # entities and enums
│   │   ├── Dtos/                              # request/response contracts + validation attributes
│   │   ├── Mapping/Mappings.cs                # entity -> DTO (manual, explicit)
│   │   ├── Middleware/                        # global exception handler -> consistent error body
│   │   ├── Exceptions/                        # NotFound / Conflict / BadRequest / Unauthorized
│   │   └── Infrastructure/                    # options, claims helper, trash purge background service
│   ├── src/TodoMcpServer/                     # MCP server (stdio) that adapts Claude Desktop to the REST API
│   └── tests/
│       ├── TodoApp.Tests/
│       │   ├── Services/                      # unit tests of every service (real SQLite in memory + real migrations)
│       │   └── Integration/                   # WebApplicationFactory tests of the real HTTP pipeline
│       └── TodoMcpServer.Tests/               # MCP server tests with a mocked HttpMessageHandler
└── frontend/
    ├── angular.json  package.json  scripts/check-contrast.mjs
    └── src/
        ├── index.html                         # applies the saved/OS theme before first paint
        ├── styles.scss  styles/               # _themes.scss (all colours), _base.scss, _components.scss
        ├── environments/                      # apiUrl for dev (localhost:5080) and production (/api)
        └── app/
            ├── app.config.ts  app.routes.ts   # providers, interceptors, lazy routes
            ├── core/                          # models, auth/theme/toast/confirm/layout services,
            │                                  # interceptors, guards, global error handler, date utils
            ├── shared/                        # icon, toast container, confirm dialog, theme picker/menu,
            │                                  # progress bar, empty state
            ├── layout/                        # shell: collapsible sidebar + top bar
            └── features/
                ├── auth/                      # login, register
                ├── sections/                  # sections store, create/edit dialog
                ├── tasks/                     # task list page, task item, task form, tags store, API service
                └── settings/                  # theme picker, account, tag management
```

## Prerequisites

* **.NET SDK 8 or newer.** The projects target `net8.0`. If only a newer runtime (9/10) is installed,
  `<RollForward>Major</RollForward>` lets them run on it.
* **Node.js 22.22.3+, 24.15+ or 26+** (required by Angular 22) and npm.
* No database server is needed: SQLite creates `todoapp.db` automatically.

## Run it

### 1. Backend (http://localhost:5080)

```powershell
cd backend
dotnet restore
dotnet run --project src/TodoApp.Api
```

In the `Development` environment (the default for `dotnet run`) the app applies migrations on start, so the database is
created automatically. Swagger UI opens at **http://localhost:5080/swagger**. Use *Authorize* and paste the
`accessToken` from `/api/auth/login` or `/api/auth/register`.

### 2. Frontend (http://localhost:4200)

```powershell
cd frontend
npm install
npm start
```

Open http://localhost:4200, create an account, and you land on *All tasks* with **Home** and **Work** ready.

### EF Core migrations

```powershell
cd backend
dotnet tool restore                                   # installs dotnet-ef 8.0.34 from dotnet-tools.json

# apply (also happens automatically on start when Database:AutoMigrate = true)
dotnet ef database update --project src/TodoApp.Api

# after changing entities
dotnet ef migrations add <Name> --project src/TodoApp.Api --output-dir Data/Migrations
```

If you run the tool on a machine that only has a newer .NET runtime, prefix with `$env:DOTNET_ROLL_FORWARD="Major"`.

### Tests

```powershell
cd backend;  dotnet test                              # 69 API tests + 78 MCP server tests
cd frontend; npm test -- --no-watch                   # 73 tests
cd frontend; node scripts/check-contrast.mjs          # WCAG AA check of every theme palette
```

### Use it from Claude Desktop (MCP)

`backend/src/TodoMcpServer` is an MCP server that lets Claude Desktop add, list, update, complete and delete tasks by calling this
API ("add 'fix the leaking tap' to my Home list, due Saturday, high priority"). Setup, the Claude Desktop config and
troubleshooting are in [backend/src/TodoMcpServer/README.md](backend/src/TodoMcpServer/README.md).

### Production build

```powershell
cd frontend; npm run build                            # -> dist/todo-app (apiUrl is "/api": serve both behind one host)
cd backend;  dotnet publish src/TodoApp.Api -c Release
```

## Configuration

| Setting | Where | Notes |
|---|---|---|
| `Jwt:Key` | user-secrets / env var `Jwt__Key` | **Required, 32+ chars.** The app refuses to start without it. `appsettings.Development.json` contains a dev-only key, never use it in production. |
| `Jwt:Issuer`, `Jwt:Audience`, `Jwt:ExpiryMinutes` | `appsettings.json` | Defaults: `TodoApp`, `TodoApp.Client`, 120 |
| `ConnectionStrings:Default` | `appsettings.json` | Default `Data Source=todoapp.db` |
| `Database:Provider` | `appsettings.json` | `Sqlite` (default) or `SqlServer` |
| `Database:AutoMigrate` | `appsettings.json` | `true` in Development, `false` otherwise |
| `Cors:AllowedOrigins` | `appsettings.json` | Defaults to `["http://localhost:4200"]` |
| `Security:BcryptWorkFactor` | `appsettings.json` | Default 11 |
| `Trash:RetentionHours` | `appsettings.json` | How long deleted tasks stay restorable (default 24) |

Set a real key for production:

```powershell
cd backend/src/TodoApp.Api
dotnet user-secrets set "Jwt:Key" "<a-long-random-string-of-at-least-32-characters>"
```

### Switching to SQL Server

1. Set `Database:Provider` to `SqlServer` and `ConnectionStrings:Default` to your connection string.
2. The checked-in migration was generated for SQLite. Generate SQL Server migrations once:
   delete `backend/src/TodoApp.Api/Data/Migrations`, then `dotnet ef migrations add InitialCreate --project src/TodoApp.Api --output-dir Data/Migrations`
   (with the provider set to SqlServer), and `dotnet ef database update`.

The model avoids SQL Server's "multiple cascade paths" error (user to tags/tasks use `Restrict`), and all queries are provider neutral.

## Architecture

**Layers.** `Controllers` (HTTP only) → `Services` (all business rules, take the user id explicitly) → `AppDbContext`
(EF Core). DTOs carry validation attributes; entities never leave the service layer. Mapping is manual and explicit.

**Security.** Passwords are hashed with BCrypt. JWTs are validated for issuer, audience, signature and lifetime.
Every query is scoped by the authenticated user's id, and referenced ids (section, tags, sub-tasks) are verified to
belong to that user, so one user can never read or change another's data (covered by tests). Unknown users and wrong
passwords are indistinguishable (same message, equalised timing).

**Errors.** Every non-2xx response has the same shape:

```json
{ "status": 400, "code": "validation_failed", "message": "One or more validation errors occurred.",
  "errors": { "title": ["The Title field is required."] }, "traceId": "0HN..." }
```

Codes: `validation_failed`, `bad_request`, `unauthorized`, `forbidden`, `not_found`, `conflict`, `server_error`.
Unexpected exceptions are logged with a stack trace (Serilog, structured) and returned as a generic message.

**Soft delete and Undo.** Deleting a task sets `DeletedAt`; `POST /api/tasks/restore` brings it back. A background
service permanently purges tasks after `Trash:RetentionHours`.

**Ordering.** Each task has a `SortOrder` within its section. `PUT /api/tasks/reorder` accepts the ids in their new
order and also works for a filtered subset: other tasks keep their slots.

**Frontend state.** Signals everywhere. `SectionsStore` and `TagsStore` cache lists; the task page owns its filter
state and re-queries through `toObservable` + `switchMap` (stale requests are cancelled; search is debounced).
An HTTP interceptor adds the JWT, a second one normalises errors (401 → sign out, network/5xx → toast), and a global
`ErrorHandler` catches the rest.

### Themes

All colours are CSS custom properties in `frontend/src/styles/_themes.scss`, switched by `<html data-theme="…">`.
Components only use `var(--token)`. Themes: **Light, Dark, Ocean, Forest, Sunset**. Resolution order:
saved choice in `localStorage` → OS `prefers-color-scheme` on a first visit. After sign-in the server copy wins, and every
change is saved to `PUT /api/preferences`. `index.html` applies the theme before the first paint (no flash).
The theme picker's swatches render with each theme's real variables (each card carries its own `data-theme`).
`scripts/check-contrast.mjs` verifies all text/background pairs against WCAG AA.

### Accessibility

Skip link and landmarks; visible focus ring in every theme; native radios/checkboxes for pickers; dialogs trap focus and
close on Esc; live regions announce toasts and list changes; overdue is shown with an icon and text, not colour alone;
keyboard re-ordering with **Alt + ↑/↓** on a task's drag handle (also in each task's “⋯” menu);
`prefers-reduced-motion` is respected.

## API reference

All endpoints except `register`/`login`/`health` require `Authorization: Bearer <token>`. Interactive docs: `/swagger`.

| Method | Path | Description |
|---|---|---|
| POST | `/api/auth/register` | Create account (+ Home and Work sections), returns token, user and preferences. Optional `theme`. |
| POST | `/api/auth/login` | Sign in |
| GET | `/api/auth/me` | Current user + preferences |
| GET | `/api/sections` | Sections with counts (`totalCount`, `pendingCount`, `inProgressCount`, `completedCount`) |
| POST | `/api/sections` | Create `{name, color: "#rrggbb", icon}` |
| GET / PUT | `/api/sections/{id}` | Read / rename, recolor, change icon |
| DELETE | `/api/sections/{id}?moveTasksTo={id}` | Delete a custom section (default sections can't be deleted). Without `moveTasksTo` its tasks are deleted too |
| PUT | `/api/sections/reorder` | `{orderedIds}` |
| GET | `/api/tasks` | Search/filter/sort/page, see below |
| POST | `/api/tasks` | Create (with optional `tagIds`, `subTasks`) |
| GET / PUT | `/api/tasks/{id}` | Read / update (optional `subTasks` array syncs the checklist) |
| DELETE | `/api/tasks/{id}` | Soft-delete |
| PATCH | `/api/tasks/{id}/completion` | `{isCompleted}` |
| POST | `/api/tasks/{id}/move` | `{sectionId}` |
| PUT | `/api/tasks/reorder` | `{orderedIds}` (ids within one section) |
| POST | `/api/tasks/restore` | `{ids}`, undo a delete |
| POST | `/api/tasks/bulk/complete` | `{ids, isCompleted}` |
| POST | `/api/tasks/bulk/delete` | `{ids}` |
| POST | `/api/tasks/{taskId}/subtasks` | `{title}` |
| PUT / DELETE | `/api/tasks/{taskId}/subtasks/{id}` | `{title, isCompleted}` / remove |
| GET / POST | `/api/tags` | List (with `taskCount`) / create `{name, color}` |
| PUT / DELETE | `/api/tags/{id}` | Rename, recolor / delete |
| GET / PUT | `/api/preferences` | `{theme, sidebarCollapsed}` |
| GET | `/health` | Liveness (anonymous) |

**`GET /api/tasks` query parameters**

| Param | Values |
|---|---|
| `search` | Case-insensitive text in title or notes (wildcards are escaped) |
| `status` | `Pending`, `InProgress`, `Completed` |
| `priority` | `Low`, `Medium`, `High` |
| `due` | `Today`, `Upcoming` (after today), `Overdue` (before today, not completed), `None` |
| `tagId`, `sectionId` | ids |
| `sortBy` / `sortDir` | `Manual` (default), `DueDate`, `Priority`, `Created`, `Title` / `Asc`, `Desc` (defaults: due date asc, priority high first, created newest first, title A–Z; undated tasks always last) |
| `page`, `pageSize` | default 1 and 50, max 200 |
| `today` | `yyyy-MM-dd`, the caller's local date for `due` (the web app sends it; defaults to the UTC date) |

Response: `{ items, page, pageSize, totalCount, totalPages }`. Enums are strings; dates are `yyyy-MM-dd`; timestamps are UTC ISO 8601.

## Notes and trade-offs

* **Token storage.** The JWT is kept in `localStorage` so reloads stay signed in. That is readable by injected scripts;
  ship a strict CSP, or move to an httpOnly cookie for a hardened deployment.
* **Not included (natural next steps):** refresh tokens, email verification / password reset, rate limiting on the auth
  endpoints, account deletion (the model deliberately uses `Restrict` from users to tasks/tags, so it needs an explicit clean-up step),
  and end-to-end browser tests.
* **Integration-test shim.** `Integration/PipeWriterCompatStartupFilter.cs` exists only because the `net8.0` test host and a
  newer shared runtime (here .NET 10) disagree about a pipe-writer API. It is harmless on a real .NET 8 runtime and does not
  affect the app.
