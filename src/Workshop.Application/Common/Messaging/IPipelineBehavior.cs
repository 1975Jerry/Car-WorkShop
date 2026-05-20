namespace Workshop.Application.Common.Messaging;

public delegate Task<TResponse> RequestHandlerDelegate<TResponse>(CancellationToken cancellationToken = default);

public interface IPipelineBehavior<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken);
}
