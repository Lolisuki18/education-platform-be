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
            var connectionString = configuration.GetConnectionString("Server");

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

            services.AddScoped<Application.Interface.IPaymentService, Infrastructure.Services.PayOSPaymentService>();
            services.AddScoped<Application.Interface.IEmailService, Infrastructure.Services.SmtpEmailService>();
            services.AddScoped<Domain.Common.Interfaces.INotificationService,
                               Infrastructure.Services.LogNotificationService>();

            services.AddScoped<Application.Interface.IStorageService, Infrastructure.Services.StorageService>();

            // Register background storage cleanup service
            services.AddHostedService<Infrastructure.Services.StorageCleanupService>();

            return services;
        }
    }
}

