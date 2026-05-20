namespace Workshop.Application.Common.Messaging;

// Void marker for request types that return no value. Handlers implement
// IRequestHandler<TRequest> (single-arg) and return Task; the dispatcher
// adapts that to Task<Unit> via the default interface bridge below.
public readonly struct Unit : IEquatable<Unit>
{
    public static readonly Unit Value = default;
    public static readonly Task<Unit> Task = System.Threading.Tasks.Task.FromResult(Value);

    public bool Equals(Unit other) => true;
    public override bool Equals(object? obj) => obj is Unit;
    public override int GetHashCode() => 0;
    public override string ToString() => "()";

    public static bool operator ==(Unit _, Unit __) => true;
    public static bool operator !=(Unit _, Unit __) => false;
}
