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

        // This project is a reserved scaffold for the eventual REST-API split.
        // External portal flows currently run inside Workshop.Web; the endpoints
        // below are placeholders so the project still compiles + responds to /health.
        app.MapGet("/health", () => Results.Ok(new { status = "ok", scaffold = true }));
        app.MapGet("/api/v1/cases", () => Results.Ok(Array.Empty<object>()))
           .RequireAuthorization();
        app.MapGet("/api/v1/parts", () => Results.Ok(Array.Empty<object>()))
           .RequireAuthorization();

        app.Run();
    }
}
