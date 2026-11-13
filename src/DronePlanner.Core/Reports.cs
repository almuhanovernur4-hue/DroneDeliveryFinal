namespace DronePlanner.Core;

public sealed record FleetRow(string DroneId, string Zone, decimal Charge, DroneStatus Status, decimal RiskIndex);
public sealed record ZoneSummary(string Zone, int DroneCount, decimal TotalCharge, decimal AverageCharge, int BookedDeliveries, decimal Revenue);

public static class FleetReports
{
    // Useful report #1: dispatch availability and risk, deterministic ordering incl tie-breaker.
    public static FleetRow[] Availability(FleetSnapshot snapshot, string? zone = null)
        => snapshot.Drones
            .Where(d => zone is null || d.Zone.Equals(zone, StringComparison.OrdinalIgnoreCase))
            .Select(d => new FleetRow(d.Id, d.Zone, d.Charge, d.Status,
                d.Status == DroneStatus.Available ? PricingPolicies.LowRisk(d.MaxPayloadKg) : PricingPolicies.HighRisk(d.MaxPayloadKg)))
            .OrderByDescending(x => x.Charge)
            .ThenBy(x => x.DroneId, StringComparer.OrdinalIgnoreCase)
            .ToArray();

    // Useful report #2: fleet utilisation, completed revenue and zone-level aggregation.
    public static ZoneSummary[] ZonePerformance(FleetSnapshot snapshot)
        => snapshot.Drones.GroupBy(d => d.Zone, StringComparer.OrdinalIgnoreCase)
            .Select(group => new ZoneSummary(group.Key,
                group.Count(), group.Sum(d => d.Charge), group.Average(d => d.Charge),
                snapshot.Plans.Count(p => p.Zone.Equals(group.Key, StringComparison.OrdinalIgnoreCase)
                    && p.Status == DeliveryStatus.Planned),
                snapshot.Plans.Where(p => p.Zone.Equals(group.Key, StringComparison.OrdinalIgnoreCase)
                    && p.Status == DeliveryStatus.Completed).Sum(p => p.TotalCost)))
            .OrderByDescending(g => g.Revenue)
            .ThenBy(g => g.Zone, StringComparer.OrdinalIgnoreCase).ToArray();
}
