namespace DronePlanner.Core;

public enum DroneStatus { Available, Reserved }
public enum DeliveryStatus { Planned, Completed, Cancelled }
public enum PricingTier { Standard, Express }

// Private constructors require use of validated factories. Records remain immutable.
public sealed record Drone
{
    public string Id { get; }
    public string Zone { get; }
    public decimal Charge { get; }
    public decimal MaxPayloadKg { get; }
    public DroneStatus Status { get; }
    private Drone(string id, string zone, decimal charge, decimal maxPayloadKg, DroneStatus status)
        => (Id, Zone, Charge, MaxPayloadKg, Status) = (id, zone, charge, maxPayloadKg, status);

    public static Result<Drone> Create(string? id, string? zone, decimal charge, decimal payload, DroneStatus status = DroneStatus.Available)
    {
        if (string.IsNullOrWhiteSpace(id)) return Result<Drone>.Failure(new PlannerError.InvalidInput("Drone.Id", "must not be empty"));
        if (string.IsNullOrWhiteSpace(zone)) return Result<Drone>.Failure(new PlannerError.InvalidInput("Drone.Zone", "must not be empty"));
        if (charge < 0m || charge > 100m) return Result<Drone>.Failure(new PlannerError.InvalidInput("Drone.Charge", "must be between 0 and 100"));
        if (payload <= 0m || payload > 100m) return Result<Drone>.Failure(new PlannerError.InvalidInput("Drone.MaxPayloadKg", "must be in (0,100]"));
        if (!Enum.IsDefined(status)) return Result<Drone>.Failure(new PlannerError.InvalidInput("Drone.Status", "unsupported value"));
        return Result<Drone>.Success(new Drone(id.Trim(), zone.Trim(), charge, payload, status));
    }

    internal Drone WithCharge(decimal charge) => new(Id, Zone, charge, MaxPayloadKg, Status);
    internal Drone WithStatus(DroneStatus status) => new(Id, Zone, Charge, MaxPayloadKg, status);
}

public sealed record DeliveryRequest
{
    public string Id { get; }
    public string Zone { get; }
    public decimal DistanceKm { get; }
    public decimal WeightKg { get; }
    private DeliveryRequest(string id, string zone, decimal distanceKm, decimal weightKg)
        => (Id, Zone, DistanceKm, WeightKg) = (id, zone, distanceKm, weightKg);
    public static Result<DeliveryRequest> Create(string? id, string? zone, decimal distance, decimal weight)
    {
        if (string.IsNullOrWhiteSpace(id)) return Result<DeliveryRequest>.Failure(new PlannerError.InvalidInput("Request.Id", "must not be empty"));
        if (string.IsNullOrWhiteSpace(zone)) return Result<DeliveryRequest>.Failure(new PlannerError.InvalidInput("Request.Zone", "must not be empty"));
        if (distance <= 0m || distance > 200m) return Result<DeliveryRequest>.Failure(new PlannerError.InvalidInput("Request.DistanceKm", "must be in (0,200]"));
        if (weight <= 0m || weight > 100m) return Result<DeliveryRequest>.Failure(new PlannerError.InvalidInput("Request.WeightKg", "must be in (0,100]"));
        return Result<DeliveryRequest>.Success(new DeliveryRequest(id.Trim(), zone.Trim(), distance, weight));
    }
}

public sealed record DeliveryPlan
{
    public string RequestId { get; }
    public string DroneId { get; }
    public string Zone { get; }
    public decimal DistanceKm { get; }
    public decimal RequiredCharge { get; }
    public PricingTier Tier { get; }
    public decimal BaseCost { get; }
    public decimal NativeSurcharge { get; }
    public decimal TotalCost => BaseCost + NativeSurcharge;
    public DeliveryStatus Status { get; }
    private DeliveryPlan(string requestId, string droneId, string zone, decimal distance, decimal requiredCharge,
        PricingTier tier, decimal baseCost, decimal nativeSurcharge, DeliveryStatus status)
        => (RequestId, DroneId, Zone, DistanceKm, RequiredCharge, Tier, BaseCost, NativeSurcharge, Status)
         = (requestId, droneId, zone, distance, requiredCharge, tier, baseCost, nativeSurcharge, status);
    internal static DeliveryPlan Create(DeliveryRequest order, Drone drone, decimal charge, PricingTier tier, decimal cost)
        => new(order.Id, drone.Id, order.Zone, order.DistanceKm, charge, tier, cost, 0m, DeliveryStatus.Planned);
    internal DeliveryPlan WithSurcharge(decimal surcharge) => new(RequestId, DroneId, Zone, DistanceKm, RequiredCharge, Tier, BaseCost, surcharge, Status);
    internal DeliveryPlan WithStatus(DeliveryStatus status) => new(RequestId, DroneId, Zone, DistanceKm, RequiredCharge, Tier, BaseCost, NativeSurcharge, status);

    public static Result<DeliveryPlan> Restore(string? requestId, string? droneId, string? zone, decimal distance,
        decimal requiredCharge, PricingTier tier, decimal baseCost, decimal nativeSurcharge, DeliveryStatus status)
    {
        if (string.IsNullOrWhiteSpace(requestId) || string.IsNullOrWhiteSpace(droneId) || string.IsNullOrWhiteSpace(zone))
            return Result<DeliveryPlan>.Failure(new PlannerError.InvalidInput("Plan", "IDs and zone are required"));
        if (distance <= 0m || distance > 200m || requiredCharge != distance * 2m || baseCost < 0m || nativeSurcharge < 0m)
            return Result<DeliveryPlan>.Failure(new PlannerError.InvalidInput("Plan", "invalid distance, charge or cost"));
        if (!Enum.IsDefined(tier) || !Enum.IsDefined(status))
            return Result<DeliveryPlan>.Failure(new PlannerError.InvalidInput("Plan", "invalid enum value"));
        return Result<DeliveryPlan>.Success(new DeliveryPlan(requestId, droneId, zone, distance, requiredCharge, tier, baseCost, nativeSurcharge, status));
    }
}

public sealed class FleetSnapshot
{
    private readonly Drone[] _drones;
    private readonly DeliveryPlan[] _plans;
    public IReadOnlyList<Drone> Drones => Array.AsReadOnly(_drones);
    public IReadOnlyList<DeliveryPlan> Plans => Array.AsReadOnly(_plans);
    private FleetSnapshot(Drone[] drones, DeliveryPlan[] plans) => (_drones, _plans) = (drones, plans);

    public static Result<FleetSnapshot> Create(IEnumerable<Drone> drones, IEnumerable<DeliveryPlan>? plans = null)
    {
        if (drones is null) return Result<FleetSnapshot>.Failure(new PlannerError.InvalidInput("Drones", "collection is null"));
        var ds = drones.ToArray();
        var ps = (plans ?? Array.Empty<DeliveryPlan>()).ToArray();
        if (ds.Any(d => d is null) || ps.Any(p => p is null))
            return Result<FleetSnapshot>.Failure(new PlannerError.InvalidInput("Data", "null model"));
        if (ds.Select(x => x.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != ds.Length ||
            ps.Select(x => x.RequestId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != ps.Length)
            return Result<FleetSnapshot>.Failure(new PlannerError.InvalidInput("IDs", "duplicates are prohibited"));
        if (ps.Any(p => !ds.Any(d => d.Id.Equals(p.DroneId, StringComparison.OrdinalIgnoreCase))))
            return Result<FleetSnapshot>.Failure(new PlannerError.InvalidInput("Plans", "references unknown drone"));
        if (ps.Where(p => p.Status == DeliveryStatus.Planned).GroupBy(p => p.DroneId, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1))
            return Result<FleetSnapshot>.Failure(new PlannerError.InvalidInput("Plans", "multiple active plans for one drone"));
        if (ds.Any(d => d.Status == DroneStatus.Reserved != ps.Any(p => p.DroneId.Equals(d.Id, StringComparison.OrdinalIgnoreCase) && p.Status == DeliveryStatus.Planned)))
            return Result<FleetSnapshot>.Failure(new PlannerError.InvalidInput("Fleet", "drone status does not match planned deliveries"));
        return Result<FleetSnapshot>.Success(new FleetSnapshot(ds.ToArray(), ps.ToArray()));
    }

    internal FleetSnapshot Change(Drone updated, DeliveryPlan? plan = null, bool append = false)
    {
        Drone[] drones = _drones.Select(d => d.Id.Equals(updated.Id, StringComparison.OrdinalIgnoreCase) ? updated : d).ToArray();
        DeliveryPlan[] plans = plan is null ? _plans.ToArray() : append ? _plans.Append(plan).ToArray()
            : _plans.Select(p => p.RequestId.Equals(plan.RequestId, StringComparison.OrdinalIgnoreCase) ? plan : p).ToArray();
        return new FleetSnapshot(drones, plans);
    }
}

public static class DomainQueries
{
    public static Option<Drone> FindDrone(this FleetSnapshot snapshot, string id)
        => snapshot.Drones.FirstOrDefault(d => d.Id.Equals(id, StringComparison.OrdinalIgnoreCase)) is { } found
            ? new Option<Drone>.Some(found) : new Option<Drone>.None();
    public static Option<DeliveryPlan> FindPlan(this FleetSnapshot snapshot, string id)
        => snapshot.Plans.FirstOrDefault(p => p.RequestId.Equals(id, StringComparison.OrdinalIgnoreCase)) is { } found
            ? new Option<DeliveryPlan>.Some(found) : new Option<DeliveryPlan>.None();
    public static Option<DeliveryRequest> FindOrder(this IEnumerable<DeliveryRequest> orders, string id)
        => orders.FirstOrDefault(o => o.Id.Equals(id, StringComparison.OrdinalIgnoreCase)) is { } found
            ? new Option<DeliveryRequest>.Some(found) : new Option<DeliveryRequest>.None();
    public static IEnumerable<Drone> InZone(this IEnumerable<Drone> drones, string zone)
        => drones.Where(d => d.Zone.Equals(zone, StringComparison.OrdinalIgnoreCase));

    // Best drone for an order: available, same zone, enough payload; highest charge first, ID as tie-breaker.
    public static Option<Drone> FindBestDrone(this FleetSnapshot snapshot, DeliveryRequest request)
        => snapshot.Drones.InZone(request.Zone)
            .Where(d => d.Status == DroneStatus.Available && d.MaxPayloadKg >= request.WeightKg)
            .OrderByDescending(d => d.Charge).ThenBy(d => d.Id, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault() is { } found
            ? new Option<Drone>.Some(found) : new Option<Drone>.None();

    // Orders that may still be booked: never planned, or planned and then cancelled. Lazy (deferred) filter.
    public static IEnumerable<DeliveryRequest> PendingOrders(this FleetSnapshot snapshot, IEnumerable<DeliveryRequest> orders)
        => orders.Where(o => snapshot.FindPlan(o.Id) is not Option<DeliveryPlan>.Some { Value.Status: not DeliveryStatus.Cancelled });
}
