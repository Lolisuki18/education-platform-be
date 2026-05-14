using AutoMapper;
using Application.Implementation;
using Application.Interface;
using Microsoft.Extensions.DependencyInjection;

namespace Application
{
    public static class ApplicationDI
    {
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            services.AddAutoMapper(typeof(ApplicationDI).Assembly);

            // Register MediatR
            services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(ApplicationDI).Assembly));

            // Đăng ký các service
            services.AddScoped<IAcademicService, AcademicService>();
            services.AddScoped<IStorageService, StorageService>();
            services.AddScoped<IStatisticService, StatisticService>();
            services.AddScoped<ISpeechToTextService, SpeechToTextService>();
            services.AddScoped<IAIService, AIService>();

            return services;
        }
    }
}
