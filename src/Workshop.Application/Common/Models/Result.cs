namespace Workshop.Application.Common.Models;

public readonly record struct Result<T>(bool Success, T? Value, string? Error, IReadOnlyList<string>? FieldErrors)
{
    public static Result<T> Ok(T value) => new(true, value, null, null);
    public static Result<T> Fail(string error) => new(false, default, error, null);
    public static Result<T> Invalid(IReadOnlyList<string> fieldErrors) =>
        new(false, default, "Validation failed", fieldErrors);
}

public readonly record struct Result(bool Success, string? Error, IReadOnlyList<string>? FieldErrors)
{
    public static Result Ok() => new(true, null, null);
    public static Result Fail(string error) => new(false, error, null);
    public static Result Invalid(IReadOnlyList<string> fieldErrors) =>
        new(false, "Validation failed", fieldErrors);
}

public record PagedList<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasNext => Page * PageSize < TotalCount;
    public bool HasPrev => Page > 1;
}
