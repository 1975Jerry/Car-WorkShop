using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Workshop.Infrastructure.Persistence;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<WorkshopDbContext>
{
    public WorkshopDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("WORKSHOP_CONNECTION_STRING")
            ?? "Host=localhost;Port=5432;Database=workshop_dev;Username=workshop;Password=workshop";

        var options = new DbContextOptionsBuilder<WorkshopDbContext>()
            .UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsAssembly(typeof(WorkshopDbContext).Assembly.GetName().Name))
            .UseSnakeCaseNamingConvention()
            .Options;

        // Migrations and schema scaffolding don't run queries, so the user
        // context is irrelevant — null user = "see all" via query filters.
        return new WorkshopDbContext(options, new NullCurrentUserService());
    }
}
