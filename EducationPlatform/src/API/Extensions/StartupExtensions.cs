using System.Text;
using Asp.Versioning;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace API.Extensions
{
    public static class StartupExtensions
    {
        public const string CorsPolicyName = "Frontend";

        /// <summary>
        /// Only the listed frontend origins may call the API from a browser. Production refuses to start without
        /// them instead of silently falling back to a localhost default.
        /// </summary>
        public static IServiceCollection AddFrontendCors(
            this IServiceCollection services,
            IConfiguration configuration,
            IHostEnvironment environment)
        {
            var origins = configuration.GetSection("CorsSettings:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();

            if (origins.Length == 0)
            {
                if (environment.IsProduction())
                    throw new InvalidOperationException(
                        "Missing configuration: CorsSettings:AllowedOrigins must list the frontend origin(s) in Production.");

                origins = new[] { "http://localhost:3000" };
            }

            services.AddCors(options =>
            {
                options.AddPolicy(CorsPolicyName, policy => policy
                    .WithOrigins(origins)
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials());
            });

            return services;
        }

        public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                var jwtSettings = configuration.GetSection("JwtSettings");
                var secretKey = jwtSettings["SecretKey"];
                var issuer = jwtSettings["Issuer"];
                var audience = jwtSettings["Audience"];

                if (string.IsNullOrWhiteSpace(secretKey))
                    throw new InvalidOperationException("Missing configuration: JwtSettings:SecretKey");
                if (Encoding.UTF8.GetByteCount(secretKey) < 32)
                    throw new InvalidOperationException(
                        "JwtSettings:SecretKey must be at least 32 bytes (256 bits) long for HS256 signing.");
                if (string.IsNullOrWhiteSpace(issuer))
                    throw new InvalidOperationException("Missing configuration: JwtSettings:Issuer");
                if (string.IsNullOrWhiteSpace(audience))
                    throw new InvalidOperationException("Missing configuration: JwtSettings:Audience");

                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = issuer,
                    ValidAudience = audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
                    ClockSkew = TimeSpan.Zero
                };

                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        // Read JWT from cookie (For Web browsers)
                        var token = context.Request.Cookies["access_token"];

                        // Read JWT from query string (For Flutter SignalR Websockets).
                        // Request logging records the path only, never the query string, so the token stays out of the logs.
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

            return services;
        }

        /// <summary>
        /// Routes keep working without a version (<c>/api/orders</c>) and are also reachable as <c>/api/v1/orders</c>.
        /// Swagger only lists the versioned routes.
        /// </summary>
        public static IServiceCollection AddApiVersioningAndSwagger(this IServiceCollection services)
        {
            services.AddApiVersioning(options =>
            {
                options.DefaultApiVersion = new ApiVersion(1, 0);
                options.AssumeDefaultVersionWhenUnspecified = true;
                options.ReportApiVersions = true;
                options.ApiVersionReader = new UrlSegmentApiVersionReader();
            })
            .AddMvc()
            .AddApiExplorer(options =>
            {
                options.GroupNameFormat = "'v'VVV";
                options.SubstituteApiVersionInUrl = true;
            });

            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "Education Platform API", Version = "v1" });
                c.EnableAnnotations();
                c.DocInclusionPredicate((_, api) => api.RelativePath?.StartsWith("api/v1/", StringComparison.OrdinalIgnoreCase) == true);

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

            return services;
        }

        /// <summary>
        /// Every endpoint can answer with a problem-details body for these statuses (see GlobalExceptionHandler),
        /// so Swagger consumers, e.g. the Flutter client generator, know about them.
        /// </summary>
        public static MvcOptions AddCommonProblemResponses(this MvcOptions options)
        {
            foreach (var status in new[]
            {
                StatusCodes.Status400BadRequest,
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status403Forbidden,
                StatusCodes.Status404NotFound,
                StatusCodes.Status409Conflict,
                StatusCodes.Status429TooManyRequests,
                StatusCodes.Status500InternalServerError
            })
            {
                options.Filters.Add(new ProducesResponseTypeAttribute(typeof(ProblemDetails), status));
            }

            return options;
        }

        /// <summary>
        /// Exports traces and metrics over OTLP when <c>OpenTelemetry:OtlpEndpoint</c> is configured
        /// (Grafana Alloy, an OpenTelemetry Collector, Jaeger, ...). Does nothing otherwise.
        /// </summary>
        public static IServiceCollection AddObservability(this IServiceCollection services, IConfiguration configuration)
        {
            var endpoint = configuration["OpenTelemetry:OtlpEndpoint"];
            if (string.IsNullOrWhiteSpace(endpoint))
                return services;

            var otlpEndpoint = new Uri(endpoint);

            services.AddOpenTelemetry()
                .ConfigureResource(resource => resource.AddService(
                    configuration["OpenTelemetry:ServiceName"] ?? "education-platform-api"))
                .WithTracing(tracing => tracing
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddOtlpExporter(o => o.Endpoint = otlpEndpoint))
                .WithMetrics(metrics => metrics
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddOtlpExporter(o => o.Endpoint = otlpEndpoint));

            return services;
        }
    }
}
