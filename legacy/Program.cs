using System;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("=== ASSIGNMENT 6: HANDLING TEMPORAL COUPLING ===");

        var state1 = BatteryFactory.Create("D1", 60m, 2m);
        var processed1 = EnergyPipeline.ProcessState(state1, PricingPolicies.StandardPolicy);
        PrintState(processed1);

        var state2 = BatteryFactory.Create("D2", 0m, 5m);
        var processed2 = EnergyPipeline.ProcessState(state2, PricingPolicies.StandardPolicy);
        PrintState(processed2);

        BatteryState unsafeState = new BatteryState.Pending("D3", 50m, 3m);
        var processedUnsafe = EnergyPipeline.ProcessState(unsafeState, PricingPolicies.StandardPolicy);
        PrintState(processedUnsafe);
    }

    private static void PrintState(BatteryState state)
    {
        switch (state)
        {
            case BatteryState.Processed p:
                Console.WriteLine($"[SUCCESS] Unit: {p.UnitId} | Final Charge: {p.FinalCharge} | Cost: {p.Cost}");
                break;
            case BatteryState.Failed f:
                Console.WriteLine($"[FAILED] Unit: {f.UnitId} | Reason: {f.Reason}");
                break;
            default:
                Console.WriteLine($"[STATE] Current state is {state.GetType().Name}");
                break;
        }
    }
}