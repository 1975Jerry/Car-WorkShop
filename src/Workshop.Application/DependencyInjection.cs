using System.Reflection;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Workshop.Application.Common.Behaviors;
using Workshop.Application.Common.Notifications;
using Workshop.Application.Features.Assessments;
using Workshop.Application.Features.InsuranceCases;

namespace Workshop.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddWorkshopApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            // Order matters: SerializeRequests must wrap ValidationBehavior so the
            // lock is held across validation + handler, not just the handler body.
            cfg.AddOpenBehavior(typeof(SerializeRequestsBehavior<,>));
            cfg.AddOpenBehavior(typeof(ValidationBehavior<,>));
        });
        services.AddValidatorsFromAssembly(assembly);
        services.AddScoped<RequestScopeLock>();
        services.AddScoped<ICaseGuardContextBuilder, CaseGuardContextBuilder>();
        services.AddScoped<IAllowedOpsValidator, AllowedOpsValidator>();
        services.AddScoped<ICaseNotificationRecipients, CaseNotificationRecipients>();
        return services;
    }
}
