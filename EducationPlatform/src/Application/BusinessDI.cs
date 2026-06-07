using AutoMapper;
using Application.Implementation;
using Application.Interface;
using Microsoft.Extensions.DependencyInjection;
using FluentValidation;

namespace Application
{
    public static class ApplicationDI
    {
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            services.AddAutoMapper(cfg => cfg.AddMaps(typeof(ApplicationDI).Assembly));

            // Register MediatR
            services.AddMediatR(cfg =>
            {
                cfg.RegisterServicesFromAssembly(typeof(ApplicationDI).Assembly);
                cfg.AddOpenBehavior(typeof(Common.Behaviors.ValidationBehavior<,>));
            });

            // Register FluentValidation
            services.AddValidatorsFromAssembly(typeof(ApplicationDI).Assembly);

            // Đăng ký các service
            services.AddScoped<IStorageService, StorageService>();
            services.AddScoped<ISpeechToTextService, SpeechToTextService>();
            services.AddScoped<IAIService, AIService>();
           

            return services;
        }
    }
}
