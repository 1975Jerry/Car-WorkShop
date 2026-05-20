using Workshop.Application.Common.Messaging;

namespace Workshop.Application.Common.Behaviors;

// Per-scope pipeline lock. In Blazor Server the IWorkshopDbContext is circuit-scoped
// and shared across page + layout + every child component. When multiple components
// fire Mediator.Send during the same render pass EF Core throws "A second operation
// was started on this context instance". We serialize requests at the pipeline level
// so handlers stay lock-free.
public class RequestScopeLock
{
    public SemaphoreSlim Gate { get; } = new(1, 1);
}

public class SerializeRequestsBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
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
