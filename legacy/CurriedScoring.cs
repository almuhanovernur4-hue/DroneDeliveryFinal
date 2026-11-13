using System;

public static class CurriedScoring
{
    public static Func<decimal, Func<decimal, Func<BatteryItem, decimal>>> CurriedScoreCalculator =>
        weight => threshold => item =>
        {
            decimal baseScore = item.InitialCharge - (item.DistanceKm * weight);
            return baseScore > threshold ? baseScore : baseScore * 0.5m;
        };

    public static readonly Func<BatteryItem, decimal> StandardScorer =
        CurriedScoreCalculator(1.5m)(40m);

    public static readonly Func<BatteryItem, decimal> AggressiveScorer =
        CurriedScoreCalculator(3.0m)(20m);
}