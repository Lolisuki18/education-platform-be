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

var builder = WebApplication.CreateBuilder(args);

// ====================
// Web API
// ====================
builder.Services.AddControllers();
builder.Services.AddHttpClient("PayOSClient")
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        SslProtocols = System.Security.Authentication.SslProtocols.Tls12
    });

// ====================
// Dependency Injection (layered)
// ====================
builder.Services.AddInfrastructure();
builder.Services.AddApplication();

// ====================
// JWT Authentication
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
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(secretKey!)
        ),
        ClockSkew = TimeSpan.Zero
    };

    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            // Read JWT from cookie
            var token = context.Request.Cookies["access_token"];
            if (!string.IsNullOrEmpty(token))
            {
                context.Token = token;
            }
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddSignalR();

var app = builder.Build();

// ====================
// Serve media files from storage
// ====================
var storageRootPath = builder.Configuration["Storage:RootPath"];

if (string.IsNullOrWhiteSpace(storageRootPath))
{
    throw new Exception("Storage:RootPath is not configured.");
}

app.UseStaticFiles();

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(storageRootPath),
    RequestPath = "/media"
});

// ====================
// DB Migration & Seeding
// ====================
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<EducationPlatformDBContext>();

    var retries = 5;
    for (int i = 0; i < retries; i++)
    {
        try
        {
            // Create DB if it doesn’t exist and apply all migrations
            await db.Database.MigrateAsync();

            // Always seed after migration
            await Seeder.SeedAsync(db);

            Console.WriteLine("Database migrated and seeded successfully.");
            break;
        }
        catch (SqlException)
        {
            Console.WriteLine($"Database not ready, retrying in 5s... ({i + 1}/{retries})");
            await Task.Delay(5000);
            if (i == retries - 1) throw;
        }
    }
}

// ====================
// Middleware
// ====================
app.UseHsts();

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// ====================
// Endpoints
// ====================
app.MapControllers();
app.MapHub<AuthHub>("/authHub");
app.MapHub<CourseHub>("/courseHub");

app.Run();
