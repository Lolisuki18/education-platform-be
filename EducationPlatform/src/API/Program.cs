using System.Data.Common;
using System.Text;
using Application;
using Infrastructure;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Seeds;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using API.Hubs;
using API.ExceptionHandlers;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// ====================
// 1. Core Configuration
// ====================
// Fix PostgreSQL DateTime issue
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

// ====================
// 2. Web API Services
// ====================
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ValidationExceptionHandler>();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

builder.Services.AddHttpClient("PayOSClient")
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        SslProtocols = System.Security.Authentication.SslProtocols.Tls12
    });

// ====================
// 3. CORS Configuration (Crucial for Flutter Web / Swagger)
// ====================
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// ====================
// 4. Dependency Injection (Layered)
// ====================
builder.Services.AddInfrastructure();
builder.Services.AddApplication();
builder.Services.AddAutoMapper(cfg => cfg.AddMaps(typeof(API.Helper.MappingProfile).Assembly));

// ====================
// 5. JWT Authentication & SignalR Setup
// ====================
var jwtSettings = builder.Configuration.GetSection("JwtSettings");
var secretKey = jwtSettings["SecretKey"];

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings["Issuer"],
        ValidAudience = jwtSettings["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey!)),
        ClockSkew = TimeSpan.Zero
    };

    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            // Read JWT from cookie (For Web browsers)
            var token = context.Request.Cookies["access_token"];

            // Read JWT from query string (For Flutter SignalR Websockets)
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;

            if (!string.IsNullOrEmpty(accessToken) &&
                (path.StartsWithSegments("/authHub") || path.StartsWithSegments("/courseHub")))
            {
                token = accessToken;
            }

            if (!string.IsNullOrEmpty(token))
            {
                context.Token = token;
            }
            return Task.CompletedTask;
        }
    };
});

// ====================
// 6. Swagger Configuration
// ====================
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Education Platform API", Version = "v1" });
    c.EnableAnnotations();

    // JWT Configuration for Swagger
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddSignalR();

// ========================================================================
// BUID THE APP
// ========================================================================
var app = builder.Build();

app.UseExceptionHandler();

// Apply CORS middleware before Authentication/Authorization
app.UseCors("AllowAll");

// ====================
// 7. Environment Specific Setup
// ====================
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Education Platform API v1");
        c.RoutePrefix = string.Empty; // Set Swagger as the root page
    });
}
else
{
    // Only use HTTPS redirection in Production to avoid Android Emulator issues
    app.UseHsts();
    app.UseHttpsRedirection();
}

// ====================
// 8. Storage & Media Files
// ====================
var storageRootPath = builder.Configuration["Storage:RootPath"];
if (string.IsNullOrWhiteSpace(storageRootPath))
{
    throw new Exception("Storage:RootPath is not configured.");
}

// Auto-create the storage directory if it doesn't exist
if (!Directory.Exists(storageRootPath))
{
    Directory.CreateDirectory(storageRootPath);
    Console.WriteLine($"[Storage] Created storage directory: {storageRootPath}");
}

app.UseStaticFiles();
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(storageRootPath),
    RequestPath = "/media"
});

// ====================
// 9. DB Migration & Seeding
// ====================
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<EducationPlatformDBContext>();

    var retries = 5;
    for (int i = 0; i < retries; i++)
    {
        try
        {
            await db.Database.MigrateAsync();
            await Seeder.SeedAsync(db);
            Console.WriteLine("[Database] Migrated and seeded successfully.");
            break;
        }
        catch (DbException ex) // Handle both PostgreSQL and SQL Server issues safely
        {
            Console.WriteLine($"[Database] Not ready, retrying in 5s... ({i + 1}/{retries}). Error: {ex.Message}");
            await Task.Delay(5000);
            if (i == retries - 1) throw;
        }
    }
}

// ====================
// 10. Middleware Pipeline
// ====================
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// ====================
// 11. Endpoints & Hubs
// ====================
app.MapControllers();
app.MapHub<AuthHub>("/authHub");
app.MapHub<CourseHub>("/courseHub");

app.Run();