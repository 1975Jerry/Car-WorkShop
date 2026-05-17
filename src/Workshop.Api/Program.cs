using Workshop.Infrastructure;

namespace Workshop.Api;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        builder.Services.AddWorkshopInfrastructure(builder.Configuration);
        builder.Services.AddAuthorization();
        builder.Services.AddOpenApi();

        var app = builder.Build();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
        }

        app.UseHttpsRedirection();
        app.UseAuthorization();

        // External portal endpoints — fleshed out in their respective phases (4 / 5 / 10).
        app.MapGet("/health", () => Results.Ok(new { status = "ok", phase = 0 }));
        app.MapGet("/api/v1/cases", () => Results.Ok(Array.Empty<object>()))
           .RequireAuthorization();
        app.MapGet("/api/v1/parts", () => Results.Ok(Array.Empty<object>()))
           .RequireAuthorization();

        app.Run();
    }
}
