using Infrastructure.Implementation;
using Domain.Common.Interfaces;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Domain.CourseManagement.Aggregate;

namespace Infrastructure
{
    public static class InfrastructureDI
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services)
        {
            // Build configuration
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false)
                .Build();

            // Get connection string
            var connectionString = configuration.GetConnectionString("Server");

            services.AddScoped<Infrastructure.Persistence.Interceptors.DomainEventDispatcherInterceptor>();

            services.AddDbContext<EducationPlatformDBContext>((sp, options) =>
            {
                var interceptor = sp.GetRequiredService<Infrastructure.Persistence.Interceptors.DomainEventDispatcherInterceptor>();
                options.UseSqlServer(connectionString)
                       .AddInterceptors(interceptor);
            });

            services.AddScoped<IAIImprovementSessionRepository, AIImprovementSessionRepository>();
            services.AddScoped<IAuditLogRepository, AuditLogRepository>();
            services.AddScoped<ICourseRepository, CourseRepository>();
            services.AddScoped<IEnrollmentRepository, EnrollmentRepository>();
            services.AddScoped<IGradeRepository, GradeRepository>();
            services.AddScoped<IOrderRepository, OrderRepository>();
            services.AddScoped<IPolicyRepository, PolicyRepository>();
            services.AddScoped<ISubjectRepository, SubjectRepository>();
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<IUnitOfWork, UnitOfWork>();

            // ----- Application Services -----
            services.AddHttpContextAccessor();
            services.AddScoped<Application.Interface.ICurrentUser, Infrastructure.Services.CurrentUser>();
            
            services.AddScoped<Application.Interface.IPaymentService, Infrastructure.Services.PayOSPaymentService>();
            services.AddScoped<Domain.Common.Interfaces.INotificationService,
                               Infrastructure.Services.LogNotificationService>();

            return services;
        }
    }
}

