using System.Collections.Generic;

public static class DeferredWorkflow
{
    public static IEnumerable<BatteryItem> StreamTelemetryWithProbe(IEnumerable<BatteryItem> source, System.Action onIterationStep)
    {
        foreach (var item in source)
        {
            onIterationStep();
            yield return item;
        }
    }
}