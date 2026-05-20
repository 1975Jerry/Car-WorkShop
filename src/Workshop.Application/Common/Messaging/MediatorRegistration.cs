using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Workshop.Application.Common.Messaging;

public static class MediatorRegistration
{
    public static IServiceCollection AddWorkshopMediator(this IServiceCollection services, params Assembly[] assemblies)
    {
        services.AddScoped<IMediator, Mediator>();

        var handlerOpenGeneric = typeof(IRequestHandler<,>);
        foreach (var assembly in assemblies)
        {
            foreach (var type in assembly.GetTypes())
            {
                if (type.IsAbstract || type.IsInterface || type.IsGenericTypeDefinition) continue;
                foreach (var iface in type.GetInterfaces())
                {
                    if (iface.IsGenericType && iface.GetGenericTypeDefinition() == handlerOpenGeneric)
                    {
                        services.AddScoped(iface, type);
                    }
                }
            }
        }

        return services;
    }

    public static IServiceCollection AddPipelineBehavior(this IServiceCollection services, Type openGenericBehavior)
    {
        if (!openGenericBehavior.IsGenericTypeDefinition)
            throw new ArgumentException("Behavior must be an open generic type definition.", nameof(openGenericBehavior));
        services.AddScoped(typeof(IPipelineBehavior<,>), openGenericBehavior);
        return services;
    }
}
