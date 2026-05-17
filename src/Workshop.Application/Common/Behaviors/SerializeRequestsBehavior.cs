using MediatR;

namespace Workshop.Application.Common.Behaviors;

/// <summary>
/// Per-scope MediatR pipeline lock. In Blazor Server the IWorkshopDbContext is
/// circuit-scoped and shared across the page, the layout, and every child
/// component on screen. When multiple components fire Mediator.Send during the
/// same render pass (e.g. each MudTab loading its own data in OnParametersSetAsync)
/// EF Core throws "A second operation was started on this context instance".
/// We serialize requests at the pipeline level so handlers stay free of locking
/// concerns and behavior is identical to a single-threaded request loop.
///
/// Registered as Scoped so the semaphore is shared per DI scope (per circuit /
/// per HTTP request) and never crosses scopes.
/// </summary>
public class RequestScopeLock
{
    public SemaphoreSlim Gate { get; } = new(1, 1);
}

public class SerializeRequestsBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly RequestScopeLock _lock;

    public SerializeRequestsBehavior(RequestScopeLock @lock) => _lock = @lock;

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        await _lock.Gate.WaitAsync(ct);
        try
        {
            return await next(ct);
        }
        finally
        {
            _lock.Gate.Release();
        }
    }
}
