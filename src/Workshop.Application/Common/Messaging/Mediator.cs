using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Workshop.Application.Common.Messaging;

public sealed class Mediator : IMediator
{
    private static readonly MethodInfo SendInternalMethod =
        typeof(Mediator).GetMethod(nameof(SendInternal), BindingFlags.NonPublic | BindingFlags.Instance)!;

    private readonly IServiceProvider _sp;

    public Mediator(IServiceProvider sp)
    {
        _sp = sp;
    }

    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var closed = SendInternalMethod.MakeGenericMethod(request.GetType(), typeof(TResponse));
        return (Task<TResponse>)closed.Invoke(this, [request, cancellationToken])!;
    }

    private Task<TResponse> SendInternal<TRequest, TResponse>(object request, CancellationToken ct)
        where TRequest : IRequest<TResponse>
    {
        var typed = (TRequest)request;
        var handler = _sp.GetRequiredService<IRequestHandler<TRequest, TResponse>>();
        var behaviors = _sp.GetServices<IPipelineBehavior<TRequest, TResponse>>().ToArray();

        // Build the chain from the inside out so the first registered behavior runs first.
        RequestHandlerDelegate<TResponse> next = c => handler.Handle(typed, c);
        for (var i = behaviors.Length - 1; i >= 0; i--)
        {
            var behavior = behaviors[i];
            var prev = next;
            next = c => behavior.Handle(typed, prev, c);
        }
        return next(ct);
    }
}
