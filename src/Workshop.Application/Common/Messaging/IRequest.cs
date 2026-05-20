namespace Workshop.Application.Common.Messaging;

// Marker for request/response messages dispatched through IMediator.
public interface IRequest<out TResponse>
{
}

// Marker for fire-and-forget commands. Equivalent to IRequest<Unit> so the
// dispatcher and pipeline behaviors don't need a parallel non-generic code path.
public interface IRequest : IRequest<Unit>
{
}
