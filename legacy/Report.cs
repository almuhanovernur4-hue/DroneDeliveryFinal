using System;
using System.Collections.Generic;
using System.Linq;

public static class Reports
{
    public record FleetStatusReportRow(string UnitId, decimal RemainingCharge, decimal Distance);

    public static IEnumerable<FleetStatusReportRow> GenerateFleetStatusReport(IEnumerable<BatteryItem> items)
    {
        if (items == null) return Enumerable.Empty<FleetStatusReportRow>();

        return items
            .Where(i => i.InitialCharge > 0 && i.DistanceKm >= 0)
            .Select(i => new FleetStatusReportRow(i.UnitId, Math.Max(0, i.InitialCharge - (i.DistanceKm * 2m)), i.DistanceKm))
            .OrderByDescending(r => r.RemainingCharge)
            .ThenBy(r => r.UnitId);]
    }

    public record EfficiencyGroupSummary(string Category, int DroneCount, decimal TotalDistance, decimal TotalInitialCharge);

    public static IEnumerable<EfficiencyGroupSummary> GenerateEfficiencySummaryReport(IEnumerable<BatteryItem> items)
    {
        if (items == null) return Enumerable.Empty<EfficiencyGroupSummary>();

        return items
            .GroupBy(i => i.DistanceKm > 50m ? "Long-Range" : "Standard-Range")
            .Select(g => new EfficiencyGroupSummary(
                Category: g.Key,
                DroneCount: g.Count(),
                TotalDistance: g.Sum(x => x.DistanceKm),
                TotalInitialCharge: g.Sum(x => x.InitialCharge)
            ))
            .OrderByDescending(s => s.TotalDistance);
    }
}