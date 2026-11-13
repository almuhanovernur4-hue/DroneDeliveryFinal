namespace DronePlanner.Core;

// State types of the pipeline. Their constructors are internal, so code outside the core cannot
// build them by hand: a ValidatedContext can only come from ValidateStage and an EnergyContext
// only from EnergyStage. Skipping a stage therefore does not compile.
public sealed record ValidatedContext
{
    public Drone Drone { get; }
    public DeliveryRequest Order { get; }
    internal ValidatedContext(Drone drone, DeliveryRequest order) => (Drone, Order) = (drone, order);
}

public sealed record EnergyContext
{
    public Drone Drone { get; }
    public DeliveryRequest Order { get; }
    public decimal RequiredCharge { get; }
    internal EnergyContext(Drone drone, DeliveryRequest order, decimal requiredCharge)
        => (Drone, Order, RequiredCharge) = (drone, order, requiredCharge);
}

public static class PricingPolicies
{
    // Curried function, partial application used in both live policies.
    public static Func<decimal, Func<EnergyContext, decimal>> CurriedDistancePrice(decimal baseFee)
        => pricePerKm => context => baseFee + pricePerKm * context.Order.DistanceKm;
    public static readonly Func<EnergyContext,decimal> Standard = CurriedDistancePrice(500m)(100m);
    public static readonly Func<EnergyContext,decimal> Express = CurriedDistancePrice(800m)(150m);

    // Second configured business rule: transport risk score for reporting.
    public static Func<decimal, Func<decimal, decimal>> CurriedRiskScore(decimal weightFactor)
        => distanceFactor => weightKg => weightKg * weightFactor + distanceFactor;
    public static readonly Func<decimal,decimal> LowRisk = CurriedRiskScore(2m)(1m);
    public static readonly Func<decimal,decimal> HighRisk = CurriedRiskScore(3m)(5m);
    public static Func<EnergyContext,decimal> Select(PricingTier tier) => tier switch
    {
        PricingTier.Standard => Standard,
        PricingTier.Express => Express,
        _ => throw new ArgumentOutOfRangeException(nameof(tier))
    };
}

public static class DeliveryPipeline
{
    public const decimal ConsumptionPerKm = 2m;
    public const decimal MinimumReserve = 10m;

    // Stage 1. Pure domain validation, including business constraints.
    public static Result<ValidatedContext> ValidateStage(Drone drone, DeliveryRequest order)
    {
        if (drone.Status != DroneStatus.Available)
            return Result<ValidatedContext>.Failure(new PlannerError.InvalidSequence(drone.Status.ToString(), "plan another delivery"));
        if (!drone.Zone.Equals(order.Zone, StringComparison.OrdinalIgnoreCase))
            return Result<ValidatedContext>.Failure(new PlannerError.Incompatible($"Drone zone {drone.Zone} differs from order zone {order.Zone}"));
        if (order.WeightKg > drone.MaxPayloadKg)
            return Result<ValidatedContext>.Failure(new PlannerError.Incompatible("Payload exceeds drone capacity"));
        return Result<ValidatedContext>.Success(new ValidatedContext(drone, order));
    }

    // Stage 2. Failure propagates before a price delegate or a native call happens.
    public static Result<EnergyContext> EnergyStage(ValidatedContext validated)
    {
        decimal consumed = validated.Order.DistanceKm * ConsumptionPerKm;
        decimal available = Math.Max(0m, validated.Drone.Charge - MinimumReserve);
        return consumed <= available
            ? Result<EnergyContext>.Success(new EnergyContext(validated.Drone, validated.Order, consumed))
            : Result<EnergyContext>.Failure(new PlannerError.InsufficientEnergy(validated.Drone.Id, consumed, available));
    }

    // Stage 3. Higher-order function: supplied pricing delegate changes a real calculation.
    public static Result<DeliveryPlan> PricingStage(EnergyContext energy, PricingTier tier, Func<EnergyContext,decimal> pricing)
    {
        decimal cost = pricing(energy);
        return cost >= 0m && cost <= 1_000_000m
            ? Result<DeliveryPlan>.Success(DeliveryPlan.Create(energy.Order, energy.Drone, energy.RequiredCharge, tier, cost))
            : Result<DeliveryPlan>.Failure(new PlannerError.InvalidInput("Price", "outside supported range"));
    }

    public static Result<DeliveryPlan> Run(Drone drone, DeliveryRequest order, PricingTier tier, Func<EnergyContext,decimal> pricing)
        => ValidateStage(drone, order)
           .Bind(EnergyStage)
           .Bind(energy => PricingStage(energy, tier, pricing)); // a failed stage skips every later stage
}

public interface INativeSurcharge
{
    Result<decimal> Calculate(decimal basePrice, decimal distanceKm);
}

public static class DeliveryWorkflows
{
    // Workflow #1: validate -> plan -> native fee -> immutable reservation.
    // The existing plan of the order (if any) decides whether booking is a legal next step:
    // no plan -> book; Cancelled -> book again, the new plan replaces the cancelled one;
    // Planned or Completed -> rejected as an invalid sequence.
    public static Result<FleetSnapshot> Book(FleetSnapshot fleet, DeliveryRequest request, string droneId,
        PricingTier tier, INativeSurcharge native, Func<EnergyContext,decimal>? overridePricing = null)
    {
        if (!Enum.IsDefined(tier)) return Result<FleetSnapshot>.Failure(new PlannerError.InvalidInput("PricingTier", "invalid tier"));
        Func<EnergyContext,decimal> pricing = overridePricing ?? PricingPolicies.Select(tier);
        return fleet.FindPlan(request.Id) switch
        {
            Option<DeliveryPlan>.Some { Value.Status: DeliveryStatus.Planned or DeliveryStatus.Completed } active
                => Result<FleetSnapshot>.Failure(new PlannerError.InvalidSequence(active.Value.Status.ToString(), "book the same order again")),
            Option<DeliveryPlan>.Some => Reserve(fleet, request, droneId, tier, native, pricing, append: false),
            _ => Reserve(fleet, request, droneId, tier, native, pricing, append: true)
        };
    }

    private static Result<FleetSnapshot> Reserve(FleetSnapshot fleet, DeliveryRequest request, string droneId,
        PricingTier tier, INativeSurcharge native, Func<EnergyContext,decimal> pricing, bool append)
        => fleet.FindDrone(droneId).ToResult("Drone", droneId)
            .Bind(drone => DeliveryPipeline.Run(drone, request, tier, pricing)
                .Bind(plan => native.Calculate(plan.BaseCost, plan.DistanceKm)
                    .Map(surcharge => fleet.Change(drone.WithStatus(DroneStatus.Reserved), plan.WithSurcharge(surcharge), append))));

    // Workflow #2: plan many orders sequentially. Iterator is intentionally lazy.
    // The caller materialises once, because enumerating twice would re-apply transitions.
    public static IEnumerable<BatchStep> BatchPlan(FleetSnapshot initial, IEnumerable<DeliveryRequest> requests,
        PricingTier tier, INativeSurcharge native)
    {
        FleetSnapshot current = initial;
        foreach (var request in requests)
        {
            if (current.FindBestDrone(request) is not Option<Drone>.Some eligible)
            {
                yield return new BatchStep(request.Id, false, "No eligible available drone", current);
                continue;
            }
            var outcome = Book(current, request, eligible.Value.Id, tier, native);
            if (outcome is Result<FleetSnapshot>.Ok ok)
            {
                current = ok.Value;
                yield return new BatchStep(request.Id, true, "Booked", current);
            }
            else if (outcome is Result<FleetSnapshot>.Fail fail)
                yield return new BatchStep(request.Id, false, fail.Error.Describe(), current);
        }
    }

    // Workflow #3: complete only an existing planned delivery; deduct energy exactly once.
    public static Result<FleetSnapshot> Complete(FleetSnapshot fleet, string requestId)
        => fleet.FindPlan(requestId).ToResult("Delivery", requestId).Bind(plan =>
        {
            if (plan.Status != DeliveryStatus.Planned)
                return Result<FleetSnapshot>.Failure(new PlannerError.InvalidSequence(plan.Status.ToString(), "complete"));
            return fleet.FindDrone(plan.DroneId).ToResult("Drone", plan.DroneId).Bind(drone =>
            {
                if (drone.Status != DroneStatus.Reserved || drone.Charge < plan.RequiredCharge)
                    return Result<FleetSnapshot>.Failure(new PlannerError.InvalidSequence(drone.Status.ToString(), "complete reserved delivery"));
                return Result<FleetSnapshot>.Success(fleet.Change(drone.WithCharge(drone.Charge - plan.RequiredCharge)
                    .WithStatus(DroneStatus.Available), plan.WithStatus(DeliveryStatus.Completed)));
            });
        });

    // Workflow #4: recharge idle drone, capped at physical battery capacity.
    public static Result<FleetSnapshot> Recharge(FleetSnapshot fleet, string droneId, decimal amount)
    {
        if (amount <= 0m || amount > 100m)
            return Result<FleetSnapshot>.Failure(new PlannerError.InvalidInput("Recharge amount", "must be in (0,100]"));
        return fleet.FindDrone(droneId).ToResult("Drone", droneId).Bind(drone =>
        {
            if (drone.Status != DroneStatus.Available)
                return Result<FleetSnapshot>.Failure(new PlannerError.InvalidSequence(drone.Status.ToString(), "recharge"));
            return Result<FleetSnapshot>.Success(fleet.Change(drone.WithCharge(Math.Min(100m, drone.Charge + amount))));
        });
    }

    // Workflow #5: cancel a planned delivery and release a reserved drone without consuming charge.
    public static Result<FleetSnapshot> Cancel(FleetSnapshot fleet, string requestId)
        => fleet.FindPlan(requestId).ToResult("Delivery", requestId).Bind(plan =>
        {
            if (plan.Status != DeliveryStatus.Planned)
                return Result<FleetSnapshot>.Failure(new PlannerError.InvalidSequence(plan.Status.ToString(), "cancel"));
            return fleet.FindDrone(plan.DroneId).ToResult("Drone", plan.DroneId)
                .Map(drone => fleet.Change(drone.WithStatus(DroneStatus.Available), plan.WithStatus(DeliveryStatus.Cancelled)));
        });

    // Additional workflow: quote the same request without modifying the fleet.
    public static Result<PriceComparison> Compare(FleetSnapshot fleet, DeliveryRequest request, string droneId)
        => fleet.FindDrone(droneId).ToResult("Drone", droneId).Bind(drone =>
            DeliveryPipeline.Run(drone, request, PricingTier.Standard, PricingPolicies.Standard).Bind(standard =>
                DeliveryPipeline.Run(drone, request, PricingTier.Express, PricingPolicies.Express)
                    .Map(express => new PriceComparison(request.Id, standard.BaseCost, express.BaseCost))));
}

public sealed record BatchStep(string RequestId, bool Success, string Message, FleetSnapshot Snapshot);
public sealed record PriceComparison(string RequestId, decimal StandardPrice, decimal ExpressPrice);
