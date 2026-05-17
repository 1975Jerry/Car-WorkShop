using Microsoft.EntityFrameworkCore;
using Workshop.Application.Common.Abstractions;
using Workshop.Infrastructure.Persistence;

namespace Workshop.Application.Tests;

internal static class TestDb
{
    public static WorkshopDbContext NewContext(string? dbName = null, ICurrentUserService? currentUser = null)
    {
        var options = new DbContextOptionsBuilder<WorkshopDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var ctx = new WorkshopDbContext(options, currentUser ?? new NullCurrentUserService());
        ctx.Database.EnsureCreated();
        return ctx;
    }
}
