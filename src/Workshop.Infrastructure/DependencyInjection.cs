using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Workshop.Application.Common.Abstractions;
using Workshop.Application.Common.Files;
using Workshop.Application.Common.MyData;
using Workshop.Application.Common.Notifications;
using Workshop.Application.Common.Pdf;
using Workshop.Domain.Entities.Identity;
using Workshop.Infrastructure.MyData;
using Workshop.Infrastructure.Notifications;
using Workshop.Infrastructure.Pdf;
using Workshop.Infrastructure.Persistence;
using Workshop.Infrastructure.Storage;

namespace Workshop.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddWorkshopInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("WorkshopDb")
            ?? throw new InvalidOperationException("Missing connection string 'WorkshopDb'.");

        services.AddSingleton(TimeProvider.System);
        services.AddScoped<AuditSaveChangesInterceptor>();
        services.AddScoped<ICurrentUserService, NullCurrentUserService>(); // Web layer overrides with HTTP-aware impl
        services.AddScoped<IBranchScopeState, NullBranchScopeState>(); // Web layer overrides with a circuit-scoped impl

        services.AddDbContext<WorkshopDbContext>((sp, options) =>
        {
            var interceptor = sp.GetRequiredService<AuditSaveChangesInterceptor>();
            options
                .UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly(typeof(WorkshopDbContext).Assembly.GetName().Name))
                .UseSnakeCaseNamingConvention()
                .AddInterceptors(interceptor);
        });
        services.AddScoped<IWorkshopDbContext>(sp => sp.GetRequiredService<WorkshopDbContext>());

        services.AddIdentityCore<User>(o =>
        {
            o.Password.RequiredLength = 8;
            o.Password.RequireNonAlphanumeric = false;
            o.Password.RequireUppercase = true;
            o.User.RequireUniqueEmail = true;
        })
        .AddRoles<Role>()
        .AddEntityFrameworkStores<WorkshopDbContext>();

        services.AddSingleton<IFileStore, LocalFileStore>();
        services.AddScoped<IQuotePdfGenerator, QuotePdfGenerator>();

        services.AddSingleton<IEmailSender, LoggingEmailSender>();
        services.AddSingleton<ISmsSender, LoggingSmsSender>();
        services.AddScoped<INotificationDispatcher, NotificationDispatcher>();

        services.AddSingleton<IMyDataClient, StubMyDataClient>();

        return services;
    }
}
