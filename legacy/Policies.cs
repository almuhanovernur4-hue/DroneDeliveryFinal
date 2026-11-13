using System;

public static class PricingPolicies
{
    public static readonly Func<decimal, decimal> StandardPolicy = distance => 500m + (100m * distance);

    public static readonly Func<decimal, decimal> ExpressPolicy = distance => 800m + (150m * distance);
}