using System.Diagnostics;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Workshop.Application.Common.Messaging;

namespace Workshop.Application.Common.Behaviors;

public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _log;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> log)
    {
        _log = log;
    }

    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var name = typeof(TRequest).Name;
        var sw = Stopwatch.StartNew();
        try
        {
            var response = await next(ct);
            sw.Stop();
            if (sw.ElapsedMilliseconds > 500)
                _log.LogWarning("MediatR {Request} completed in {Elapsed}ms (slow)", name, sw.ElapsedMilliseconds);
            else
                _log.LogDebug("MediatR {Request} completed in {Elapsed}ms", name, sw.ElapsedMilliseconds);
            return response;
        }
        catch (ValidationException vex)
        {
            sw.Stop();
            _log.LogWarning("MediatR {Request} validation failed in {Elapsed}ms: {Errors}",
                name, sw.ElapsedMilliseconds,
                string.Join("; ", vex.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}")));
            throw;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _log.LogError(ex, "MediatR {Request} threw after {Elapsed}ms", name, sw.ElapsedMilliseconds);
            throw;
        }
    }
}
