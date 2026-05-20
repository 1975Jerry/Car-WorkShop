namespace Workshop.Application.Common.Messaging;

public interface IRequestHandler<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
}

// Single-arg handler for IRequest commands. Implementers write a normal
// `Task Handle(...)`; the default interface bridge below adapts that to the
// Task<Unit> signature the dispatcher resolves through IRequestHandler<,>.
public interface IRequestHandler<in TRequest> : IRequestHandler<TRequest, Unit>
    where TRequest : IRequest
{
    new Task Handle(TRequest request, CancellationToken cancellationToken);

    async Task<Unit> IRequestHandler<TRequest, Unit>.Handle(TRequest request, CancellationToken cancellationToken)
    {
        await Handle(request, cancellationToken);
        return Unit.Value;
    }
}
