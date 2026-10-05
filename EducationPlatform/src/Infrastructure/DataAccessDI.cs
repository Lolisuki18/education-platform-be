using Infrastructure.Implementation;
using Domain.Common.Interfaces;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Domain.AcademicManagement.Aggregate;
using Domain.AuditManagement.Aggregate;
using Domain.CourseManagement.Aggregate;
using Domain.EnrollmentManagement.Aggregate;
using Domain.IdentityManagement.Aggregate;
using Domain.OrderManagement.Aggregate;

namespace Infrastructure
{
    public static class InfrastructureDI
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            // Get connection string
            // "Server" is the name older deployments use; "Default" is what the example config and EF tooling use
            var connectionString = configuration.GetConnectionString("Server") ?? configuration.GetConnectionString("Default");

            services.AddOptions<Application.Options.PayOSOptions>()
                .Bind(configuration.GetSection(Application.Options.PayOSOptions.SectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services.AddOptions<Application.Options.UploadOptions>()
                .Bind(configuration.GetSection(Application.Options.UploadOptions.SectionName));

            services.AddScoped<Infrastructure.Persistence.Interceptors.DomainEventDispatcherInterceptor>();

            services.AddDbContext<EducationPlatformDBContext>((sp, options) =>
            {
                var interceptor = sp.GetRequiredService<Infrastructure.Persistence.Interceptors.DomainEventDispatcherInterceptor>();
                options.UseNpgsql(connectionString)
                       .AddInterceptors(interceptor)
                       .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
            });

            services.AddScoped<Application.Interface.IApplicationDBContext>(sp => sp.GetRequiredService<EducationPlatformDBContext>());

            services.AddScoped<IAuditRepository, AuditLogRepository>();
            services.AddScoped<ICourseRepository, CourseRepository>();
            services.AddScoped<IComplaintRepository, ComplaintRepository>();
            services.AddScoped<ICourseReviewRepository, CourseReviewRepository>();
            services.AddScoped<IEnrollmentRepository, EnrollmentRepository>();
            services.AddScoped<IGradeRepository, GradeRepository>();
            services.AddScoped<IOrderRepository, OrderRepository>();
            services.AddScoped<ICouponRepository, CouponRepository>();
            services.AddScoped<IPolicyRepository, PolicyRepository>();
            services.AddScoped<ISubjectRepository, SubjectRepository>();
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<IUnitOfWork, UnitOfWork>();

            // ----- Application Services -----
            services.AddHttpContextAccessor();
            services.AddScoped<Application.Interface.ICurrentUser, Infrastructure.Services.CurrentUser>();
            services.AddScoped<Application.Interface.ITokenService, Infrastructure.Services.JwtTokenService>();
            services.AddMemoryCache();
            services.AddSingleton<Application.Interface.ILoginAttemptTracker, Infrastructure.Services.MemoryLoginAttemptTracker>();
            services.AddSingleton<Application.Interface.IUserActivityCache, Infrastructure.Services.MemoryUserActivityCache>();

            services.AddScoped<Application.Interface.IPaymentService, Infrastructure.Services.PayOSPaymentService>();
            services.AddScoped<Application.Interface.IPayOSSignatureVerifier, Infrastructure.Services.PayOSSignatureVerifier>();
            services.AddScoped<Application.Interface.IEmailService, Infrastructure.Services.SmtpEmailService>();
            services.AddScoped<Domain.Common.Interfaces.INotificationService,
                               Infrastructure.Services.LogNotificationService>();

            services.AddScoped<Application.Interface.IStorageService, Infrastructure.Services.StorageService>();

            // Register background services
            services.AddHostedService<Infrastructure.Services.StorageCleanupService>();

            if (configuration.GetValue("Orders:ExpiredOrderCleanupEnabled", true))
            {
                services.AddHostedService<Infrastructure.Services.ExpiredOrderCleanupService>();
            }

            return services;
        }
    }
}

