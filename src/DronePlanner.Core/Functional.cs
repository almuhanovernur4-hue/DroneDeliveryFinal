namespace DronePlanner.Core;

// Discriminated-union style outcomes; errors cannot accidentally be mistaken for values.
public abstract record Result<T>
{
    private Result() { }
    public sealed record Ok(T Value) : Result<T>;
    public sealed record Fail(PlannerError Error) : Result<T>;
    public static Result<T> Success(T value) => new Ok(value);
    public static Result<T> Failure(PlannerError error) => new Fail(error);
}

public abstract record Option<T>
{
    private Option() { }
    public sealed record Some(T Value) : Option<T>;
    public sealed record None : Option<T>;
}

public abstract record PlannerError
{
    private PlannerError() { }
    public sealed record InvalidInput(string Field, string Reason) : PlannerError;
    public sealed record Missing(string Entity, string Identifier) : PlannerError;
    public sealed record InsufficientEnergy(string DroneId, decimal Needed, decimal Available) : PlannerError;
    public sealed record InvalidSequence(string Current, string Attempted) : PlannerError;
    public sealed record Incompatible(string Explanation) : PlannerError;
    public sealed record IoFailure(string Code, string Details) : PlannerError;
    public sealed record NativeFailure(string Details) : PlannerError;
}

public static class FunctionalExtensions
{
    public static Result<U> Map<T,U>(this Result<T> source, Func<T,U> map) => source switch
    {
        Result<T>.Ok ok => Result<U>.Success(map(ok.Value)),
        Result<T>.Fail fail => Result<U>.Failure(fail.Error),
        _ => throw new ArgumentOutOfRangeException(nameof(source))
    };

    public static Result<U> Bind<T,U>(this Result<T> source, Func<T,Result<U>> bind) => source switch
    {
        Result<T>.Ok ok => bind(ok.Value),
        Result<T>.Fail fail => Result<U>.Failure(fail.Error),
        _ => throw new ArgumentOutOfRangeException(nameof(source))
    };

    public static Result<T> ToResult<T>(this Option<T> source, string entity, string id) => source switch
    {
        Option<T>.Some some => Result<T>.Success(some.Value),
        Option<T>.None => Result<T>.Failure(new PlannerError.Missing(entity,id)),
        _ => throw new ArgumentOutOfRangeException(nameof(source))
    };

    public static string Describe(this PlannerError error) => error switch
    {
        PlannerError.InvalidInput e => $"Invalid {e.Field}: {e.Reason}",
        PlannerError.Missing e => $"{e.Entity} '{e.Identifier}' was not found",
        PlannerError.InsufficientEnergy e => $"Drone {e.DroneId} requires {e.Needed} units; available after reserve: {e.Available}",
        PlannerError.InvalidSequence e => $"Cannot {e.Attempted} while state is {e.Current}",
        PlannerError.Incompatible e => e.Explanation,
        PlannerError.IoFailure e => $"{e.Code}: {e.Details}",
        PlannerError.NativeFailure e => $"Native library: {e.Details}",
        _ => "Unknown error"
    };
}
