using System;
using System.Collections.Generic;
using System.Linq;

public static class Extensions
{
    public static IEnumerable<BatteryItem> FilterHighConsumers(this IEnumerable<BatteryItem> items, decimal maxAllowedCharge)
    {
        if (items == null) yield break;
        foreach (var item in items)
        {
            if ((item.DistanceKm * 2m) <= maxAllowedCharge)
            {
                yield return item;
            }
        }
    }

    public static Result<U, TError> MapResult<T, U, TError>(this Result<T, TError> result, Func<T, U> mapper)
    {
        return result switch
        {
            Result<T, TError>.Ok ok => new Result<U, TError>.Ok(mapper(ok.Value)),
            Result<T, TError>.Fail fail => new Result<U, TError>.Fail(fail.Error),
            _ => throw new ArgumentOutOfRangeException(nameof(result))
        };
    }
}