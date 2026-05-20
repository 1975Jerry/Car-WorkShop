using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Workshop.Application.Common.Behaviors;
using Workshop.Application.Common.Messaging;
using Workshop.Application.Common.Notifications;
using Workshop.Application.Features.Assessments;
using Workshop.Application.Features.InsuranceCases;

namespace Workshop.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddWorkshopApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();
        services.AddWorkshopMediator(assembly);
        // Order matters: SerializeRequests must wrap everything so the lock is held
        // across logging + validation + handler. Logging wraps Validation so
        // ValidationException is logged at WARN level alongside other failures.
        services.AddPipelineBehavior(typeof(SerializeRequestsBehavior<,>));
        services.AddPipelineBehavior(typeof(LoggingBehavior<,>));
        services.AddPipelineBehavior(typeof(ValidationBehavior<,>));
        services.AddValidatorsFromAssembly(assembly);
        services.AddScoped<RequestScopeLock>();
        services.AddScoped<ICaseGuardContextBuilder, CaseGuardContextBuilder>();
        services.AddScoped<IAllowedOpsValidator, AllowedOpsValidator>();
        services.AddScoped<ICaseNotificationRecipients, CaseNotificationRecipients>();
        return services;
    }
}
