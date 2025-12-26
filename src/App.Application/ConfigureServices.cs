using System.Reflection;
using FluentValidation;
using Mediator;
using Microsoft.Extensions.DependencyInjection;
using App.Application.Common.Behaviors;

namespace App.Application;

public static class ConfigureServices
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddHttpClient();
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
        services.AddMediator(options =>
        {
            options.Namespace = "App.Application.Mediator";
            options.ServiceLifetime = ServiceLifetime.Scoped;
        });
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(UnhandledExceptionBehaviour<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationBehaviour<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(AuditBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(QueryLoggingBehavior<,>));
        return services;
    }
}
