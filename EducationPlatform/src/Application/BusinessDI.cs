using AutoMapper;

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
                cfg.AddOpenBehavior(typeof(Common.Behaviors.CachingBehavior<,>));
                cfg.AddOpenBehavior(typeof(Common.Behaviors.SecurityEventBehavior<,>));
            });

            // Register FluentValidation
            services.AddValidatorsFromAssembly(typeof(ApplicationDI).Assembly);

            // Register services


            return services;
        }
    }
}
