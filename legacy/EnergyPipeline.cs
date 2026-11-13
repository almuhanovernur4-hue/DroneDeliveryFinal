using System;
using System.IO;

public static class EnergyPipeline
{
    public static Result<U, TError> Map<T, U, TError>(
        Result<T, TError> result,
        Func<T, U> mapper)
    {
        return result switch
        {
            Result<T, TError>.Ok ok => new Result<U, TError>.Ok(mapper(ok.Value)),
            Result<T, TError>.Fail fail => new Result<U, TError>.Fail(fail.Error),
            _ => throw new ArgumentOutOfRangeException(nameof(result))
        };
    }

    public static Result<U, TError> Bind<T, U, TError>(
        Result<T, TError> result,
        Func<T, Result<U, TError>> binder)
    {
        return result switch
        {
            Result<T, TError>.Ok ok => binder(ok.Value),
            Result<T, TError>.Fail fail => new Result<T, TError>.Fail(fail.Error),
            _ => throw new ArgumentOutOfRangeException(nameof(result))
        };
    }

    public static Result<BatteryItem, DomainError> ValidateCharge(BatteryItem item)
    {
        if (item.InitialCharge <= 0)
            return new Result<BatteryItem, DomainError>.Fail(new DomainError("ERR_CHARGE", "Initial charge must be greater than zero."));
        return new Result<BatteryItem, DomainError>.Ok(item);
    }

    public static Result<BatteryItem, DomainError> ValidateDistance(BatteryItem item)
    {
        if (item.DistanceKm < 0)
            return new Result<BatteryItem, DomainError>.Fail(new DomainError("ERR_DISTANCE", "Distance cannot be negative."));
        return new Result<BatteryItem, DomainError>.Ok(item);
    }

    public static Result<string, string> ReadDataSafely(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
                return new Result<string, string>.Fail("File not found.");

            string content = File.ReadAllText(filePath);
            if (string.IsNullOrWhiteSpace(content))
                return new Result<string, string>.Fail("Malformed or empty file content.");

            return new Result<string, string>.Ok(content);
        }
        catch (Exception ex)
        {
            return new Result<string, string>.Fail($"IO Error: {ex.Message}");
        }
    }
}