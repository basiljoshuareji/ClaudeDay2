using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using TodoApp.Api.Data;
using TodoApp.Api.Dtos;
using TodoApp.Api.Infrastructure;
using TodoApp.Api.Middleware;
using TodoApp.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------- logging
builder.Host.UseSerilog((context, services, logger) => logger
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

// ---------------------------------------------------------------- options
// Options are bound lazily so values supplied late (user-secrets, env vars, test overrides) are honoured.
builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddOptions<SecurityOptions>()
    .Bind(builder.Configuration.GetSection(SecurityOptions.SectionName))
    .ValidateDataAnnotations();
builder.Services.AddOptions<TrashOptions>().Bind(builder.Configuration.GetSection(TrashOptions.SectionName));

// ---------------------------------------------------------------- database
// SQLite by default. For SQL Server set Database:Provider=SqlServer and ConnectionStrings:Default (see README).
builder.Services.AddDbContext<AppDbContext>((sp, options) =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var provider = config["Database:Provider"] ?? "Sqlite";
    var connectionString = config.GetConnectionString("Default") ?? "Data Source=todoapp.db";

    if (provider.Equals("SqlServer", StringComparison.OrdinalIgnoreCase))
    {
        options.UseSqlServer(connectionString);
    }
    else
    {
        options.UseSqlite(connectionString);
    }

    // The soft-delete query filter on tasks also touches the many-to-many join entity; that is intentional.
    options.ConfigureWarnings(w => w.Ignore(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning));
});

// ---------------------------------------------------------------- application services
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
builder.Services.AddSingleton<ITokenService, TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ISectionService, SectionService>();
builder.Services.AddScoped<ITaskService, TaskService>();
builder.Services.AddScoped<ISubTaskService, SubTaskService>();
builder.Services.AddScoped<ITagService, TagService>();
builder.Services.AddScoped<IPreferenceService, PreferenceService>();
builder.Services.AddHostedService<TrashPurgeService>();

// ---------------------------------------------------------------- authentication (JWT)
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((bearer, jwtOptions) =>
    {
        var jwt = jwtOptions.Value;
        bearer.MapInboundClaims = false; // keep "sub" as "sub"
        bearer.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "name"
        };

        // Return the same JSON error shape for 401/403 as for every other failure.
        bearer.Events = new JwtBearerEvents
        {
            OnChallenge = context =>
            {
                context.HandleResponse();
                return ApiErrorWriter.WriteAsync(context.HttpContext, StatusCodes.Status401Unauthorized,
                    "unauthorized", "Authentication is required or the token is invalid or expired.", null);
            },
            OnForbidden = context => ApiErrorWriter.WriteAsync(context.HttpContext, StatusCodes.Status403Forbidden,
                "forbidden", "You do not have access to this resource.", null)
        };
    });
builder.Services.AddAuthorization();

// ---------------------------------------------------------------- CORS (Angular dev server)
builder.Services.AddOptions<CorsOptions>().Configure<IConfiguration>((cors, config) =>
{
    var origins = config.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? new[] { "http://localhost:4200" };
    cors.AddPolicy("Angular", policy => policy
        .WithOrigins(origins)
        .AllowAnyHeader()
        .AllowAnyMethod());
});
builder.Services.AddCors();

// ---------------------------------------------------------------- MVC, validation, JSON
builder.Services.AddControllers()
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        o.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
    })
    .ConfigureApiBehaviorOptions(o =>
    {
        // Model-validation failures use the same ApiError shape as everything else.
        o.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState
                .Where(e => e.Value is { Errors.Count: > 0 })
                .ToDictionary(
                    e => JsonNamingPolicy.CamelCase.ConvertName(e.Key.TrimStart('$', '.')),
                    e => e.Value!.Errors
                        .Select(x => string.IsNullOrEmpty(x.ErrorMessage) ? "The value is invalid." : x.ErrorMessage)
                        .ToArray());

            var body = new ApiError(StatusCodes.Status400BadRequest, "validation_failed",
                "One or more validation errors occurred.", errors, context.HttpContext.TraceIdentifier);
            return new BadRequestObjectResult(body);
        };
    });

// ---------------------------------------------------------------- Swagger / OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Todo API",
        Version = "v1",
        Description = "Sections, tasks, sub-tasks, tags and preferences for a multi-user to-do app."
    });

    var xml = Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");
    if (File.Exists(xml))
    {
        c.IncludeXmlComments(xml);
    }

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Paste the accessToken returned by /api/auth/login or /api/auth/register.",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddHealthChecks();

var app = builder.Build();

// ---------------------------------------------------------------- database migration on start (optional)
if (app.Configuration.GetValue<bool>("Database:AutoMigrate"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

// ---------------------------------------------------------------- pipeline
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Swagger:Enabled"))
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Todo API v1"));
}

app.UseCors("Angular");
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();

app.Run();

/// <summary>Exposed so integration tests can use WebApplicationFactory&lt;Program&gt;.</summary>
public partial class Program;
