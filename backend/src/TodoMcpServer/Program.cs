using System.Net.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TodoMcpServer.Api;
using TodoMcpServer.Configuration;

// ---------------------------------------------------------------------------------------------------------------------
// stdout IS the MCP protocol channel. A single stray byte there corrupts the JSON-RPC stream and Claude Desktop will drop the
// server. So: (1) redirect Console.Out to stderr as a safety net (the MCP transport writes to the real stdout stream directly,
// not through Console.Out), and (2) send ALL logging to stderr below. Never use Console.WriteLine in this project.
// ---------------------------------------------------------------------------------------------------------------------
Console.SetOut(Console.Error);

// Content root = the folder holding the executable, so appsettings.json is found even though Claude Desktop starts the process
// with an arbitrary working directory.
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory,
});

// Optional, git-ignored overrides for local development (see README). Environment variables still win.
builder.Configuration.AddJsonFile("appsettings.local.json", optional: true, reloadOnChange: false);

builder.Logging.ClearProviders();
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);
builder.Logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning); // keep request URLs out of the log

// Fail fast, with a clear message on stderr, if the configuration is unusable.
var options = TodoApiOptions.Load(builder.Configuration);
var problems = options.Validate();
if (problems.Count > 0)
{
    Console.Error.WriteLine("Todo MCP server cannot start: invalid configuration.");
    foreach (var problem in problems)
    {
        Console.Error.WriteLine($"  - {problem}");
    }

    Console.Error.WriteLine("Set the variables in the \"env\" block of the MCP server entry in claude_desktop_config.json (see README.md).");
    return 1;
}

builder.Services.AddSingleton(options);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<TodoTokenStore>(); // the JWT cache must outlive the (transient) typed client

builder.Services
    .AddHttpClient<TodoApiClient>(client =>
    {
        client.BaseAddress = new Uri(options.ApiUrl.TrimEnd('/') + "/"); // trailing slash keeps any path prefix intact
        client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("TodoMcpServer/1.0");
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
    })
    .ConfigurePrimaryHttpMessageHandler(() => CreateHandler(options));

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

var host = builder.Build();

var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("TodoMcpServer");
logger.LogInformation("Todo MCP server starting. {Options}", options);
if (options.AllowInsecureDevCert)
{
    logger.LogWarning("TODO_ALLOW_INSECURE_DEV_CERT is on: HTTPS certificate errors are ignored for {Url}. Use only for local development.", options.ApiUrl);
}

await host.RunAsync();
return 0;

static HttpMessageHandler CreateHandler(TodoApiOptions options)
{
    var handler = new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(5) };
    if (options.AllowInsecureDevCert)
    {
        // Validate() has already ensured the URL is localhost; this only skips the trust-chain check for a self-signed dev cert.
        handler.SslOptions = new SslClientAuthenticationOptions { RemoteCertificateValidationCallback = (_, _, _, _) => true };
    }

    return handler;
}
