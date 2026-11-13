using System;

public abstract record Result<T, TError>
{
    public sealed record Ok(T Value) : Result<T, TError>;
    public sealed record Fail(TError Error) : Result<T, TError>;
}
public abstract record Maybe<T>
{
    public sealed record Some(T Value) : Maybe<T>;
    public sealed record None : Maybe<T>;
}
public sealed record DomainError(string Code, string Message);

public record BatteryItem(string UnitId, decimal InitialCharge, decimal DistanceKm);