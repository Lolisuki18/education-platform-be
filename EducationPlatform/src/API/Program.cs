using API.ExceptionHandlers;
using API.Extensions;
using API.Hubs;
using API.Middlewares;
using Application;
using Infrastructure;
using Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.FileProviders;
using Serilog;

// Container health probe (the image has no curl): `dotnet API.dll --healthcheck`
if (args.Contains("--healthcheck"))
{
    return await API.Helpers.HealthProbe.RunAsync(Environment.GetEnvironmentVariable("ASPNETCORE_URLS"));
}

var builder = WebApplication.CreateBuilder(args);

// The server software and version are nobody's business
builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);

// 1. Core Configuration
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithMachineName()
        .WriteTo.Console(outputTemplate:
            "[{Timestamp:HH:mm:ss} {Level:u3}] ({CorrelationId}) {Message:lj}{NewLine}{Exception}");

    // Containers log to the console only (the log shipper collects it); a file is for local development
    if (context.Configuration.GetValue("Logging:File:Enabled", context.HostingEnvironment.IsDevelopment()))
    {
        configuration.WriteTo.File("logs/log-.txt",
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 14,
            outputTemplate:
            "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] ({CorrelationId}) {Message:lj}{NewLine}{Exception}");
    }
});

// 2. Web API Services
builder.Services.AddControllers(options => options.AddCommonProblemResponses());
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ValidationExceptionHandler>();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(API.Helpers.Policies.AdminOnly, policy => policy.RequireRole("Admin"));

    // Secure by default: an endpoint without an explicit [Authorize] / [AllowAnonymous] requires a signed-in user,
    // so forgetting the attribute on a new endpoint closes it instead of leaving it open.
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
});

builder.Services.AddForwardedHeadersSupport(builder.Configuration);
builder.Services.AddApiRateLimiting(builder.Configuration);
builder.Services.AddResponseOptimizations(builder.Configuration);

builder.Services.AddHttpClient("PayOSClient")
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        SslProtocols = System.Security.Authentication.SslProtocols.Tls12
    });

// 3. CORS (Flutter Web / Swagger UI)
builder.Services.AddFrontendCors(builder.Configuration, builder.Environment);

// 4. Dependency Injection (Layered)
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApplication();

builder.Services.AddOptions<Application.Options.NotificationOptions>()
    .Bind(builder.Configuration.GetSection(Application.Options.NotificationOptions.SectionName));
builder.Services.AddScoped<Domain.Common.Interfaces.INotificationService, API.Services.HubNotificationService>();
builder.Services.AddAutoMapper(cfg => cfg.AddMaps(typeof(API.Helpers.MappingProfile).Assembly));

// "ready" checks decide whether the instance can serve traffic; liveness (/healthz) deliberately checks nothing
builder.Services.AddHealthChecks()
    .AddDbContextCheck<EducationPlatformDBContext>("database", tags: new[] { "ready" });

// 5. JWT Authentication, API versioning & Swagger, SignalR, telemetry
builder.Services.AddJwtAuthentication(builder.Configuration);
builder.Services.AddApiVersioningAndSwagger();
builder.Services.AddSignalR();
builder.Services.AddObservability(builder.Configuration);

// BUILD THE APP
var app = builder.Build();

var logger = app.Services.GetRequiredService<ILogger<Program>>();

// 6. DB Migration & Seeding.
// Run `dotnet API.dll --migrate` as a one-off job (init container / release step) and set
// Database:AutoMigrate=false to keep migrations out of the instances that serve traffic.
if (args.Contains("--migrate"))
{
    await DatabaseInitializer.InitializeAsync(app.Services, logger);
    return 0;
}

if (app.Configuration.GetValue("Database:AutoMigrate", true))
{
    await DatabaseInitializer.InitializeAsync(app.Services, logger);
}

// 7. Middleware Pipeline
app.UseForwardedHeaders();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseSerilogRequestLogging();

app.UseExceptionHandler();

// Before the static files and the output cache, so everything text-like leaves compressed
app.UseResponseCompression();

if (app.Configuration.GetValue("Swagger:Enabled", app.Environment.IsDevelopment()))
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Education Platform API v1");
        c.RoutePrefix = string.Empty; // Set Swagger as the root page
    });
}

if (app.Configuration.GetValue("Security:UseHttpsRedirection", !app.Environment.IsDevelopment()))
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

// 8. Storage & Media Files
var storageRootPath = builder.Configuration["Storage:RootPath"];
if (string.IsNullOrWhiteSpace(storageRootPath))
{
    throw new InvalidOperationException("Missing configuration: Storage:RootPath");
}

// Auto-create the storage directory if it doesn't exist
if (!Directory.Exists(storageRootPath))
{
    Directory.CreateDirectory(storageRootPath);
    logger.LogInformation("Created storage directory: {StorageRootPath}", storageRootPath);
}

app.UseStaticFiles();

// Uploaded lesson videos need a signed link; everything else under /media stays public
app.UseMiddleware<ProtectedMediaMiddleware>();
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(storageRootPath),
    RequestPath = "/media"
});

app.UseRouting();

app.UseCors(StartupExtensions.CorsPolicyName);

app.UseAuthentication();

// After authentication so limits can be kept per user; before the user lookup so rejected requests stay cheap
app.UseRateLimiter();

app.UseMiddleware<API.Helpers.UserActiveMiddleware>();
app.UseAuthorization();

// After CORS, routing and authentication: the cache policy needs to know whether the caller is signed in
app.UseOutputCache();

// 9. Endpoints & Hubs
app.MapControllers();
app.MapHub<AuthHub>("/authHub");
app.MapHub<CourseHub>("/courseHub");
app.MapGet("/", () => "API is running successfully!").AllowAnonymous();

// Liveness: the process is up (no dependencies checked, so a database blip does not restart the container)
app.MapHealthChecks("/healthz", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();

// Readiness: dependencies are reachable, safe to route traffic here
app.MapHealthChecks("/readiness", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") }).AllowAnonymous();

app.Run();
return 0;

public partial class Program { }
