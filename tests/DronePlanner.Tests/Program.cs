using DronePlanner.Core;
using DronePlanner.App;

namespace DronePlanner.Tests;

public static class Program
{
    private static readonly (string Name, Action Run)[] Cases =
    {
        ("PURE 01 validates drone normal", DroneValid),
        ("PURE 02 rejects invalid charge boundaries", ChargeRejected),
        ("PURE 03 validates delivery distance boundaries", RequestBoundaries),
        ("PURE 04 rejects invalid empty IDs", EmptyIds),
        ("PURE 05 rejects insufficient battery reserve", InsufficientEnergy),
        ("PURE 06 rejects incompatible zone", WrongZone),
        ("PURE 07 rejects oversized payload", OversizedPayload),
        ("PURE 08 policies differ for identical input", DifferentPolicies),
        ("PURE 09 currying / partial application", Currying),
        ("PURE 10 Result Map and Bind propagate failure", MapBindPropagation),
        ("PURE 11 Option expresses missing drone", MissingLookup),
        ("PURE 12 two extensions and zone filter", ExtensionsWork),
        ("REPORT 13 empty datasets", EmptyReports),
        ("REPORT 14 deterministic tie breaker", TieBreaker),
        ("REPORT 15 grouping, aggregation and revenue", GroupedReport),
        ("WORKFLOW 16 booking and completion", BookingCompletion),
        ("WORKFLOW 17 cancellation and recharge", CancelRecharge),
        ("ORDER 18 invalid sequence leaves state untouched", SafeOrdering),
        ("ORDER 19 failed native stage preserves state", NativeFailureAtomic),
        ("LAZY 20 deferred iterator and materialised snapshot", LazyWorkflow),
        ("NATIVE 21 real PInvoke call influences price", NativeSuccess),
        ("NATIVE 22 real missing-binary loader failure", NativeMissing),
        ("FILES 23 absent JSON file", MissingJson),
        ("FILES 24 malformed JSON content", MalformedJson),
        ("FILES 25 invalid loaded domain state", InvalidLoadedData),
        ("FILES 26 save/load roundtrip", SaveAndReload),
        ("FILES 27 file handle released after load", FileDisposed),
        ("FILES 28 CSV export and empty zone report", CsvExport),
        ("STATE 29 source data not mutated", Immutability),
        ("STATE 30 duplicate request forbidden", DuplicateRequest),
        ("FAILURE 31 pricing not called after energy failure", PricingSkippedAfterFailure),
        ("FAILURE 32 invalid native inputs rejected", NativeInputRejection),
        ("PURE 33 exact energy boundary is accepted", EnergyBoundary),
        ("PURE 34 order lookup, best drone and pending orders", OrderQueries),
        ("ORDER 35 cancelled order can be booked again", RebookAfterCancel),
        ("ORDER 36 pipeline state types cannot be forged", StateTypesSealed)
    };

    public static int Main()
    {
        int passed = 0;
        foreach (var (name, run) in Cases)
        {
            try { run(); Console.WriteLine("PASS " + name); passed++; }
            catch (Exception exception) { Console.WriteLine("FAIL " + name + " -> " + exception); }
        }
        Console.WriteLine($"\nRESULT: {passed}/{Cases.Length} tests passed, {Cases.Length - passed} failed");
        return passed == Cases.Length ? 0 : 1;
    }

    private static T Ok<T>(Result<T> result) => result is Result<T>.Ok ok ? ok.Value
        : throw new Exception("Expected success: " + ((Result<T>.Fail)result).Error.Describe());
    private static PlannerError Error<T>(Result<T> result) => result is Result<T>.Fail fail ? fail.Error
        : throw new Exception("Expected domain failure");
    private static void Check(bool condition, string detail) { if (!condition) throw new Exception(detail); }
    private static Drone Drone(string id = "D01", string zone = "North", decimal charge = 95m, decimal capacity = 6m)
        => Ok(DronePlanner.Core.Drone.Create(id, zone, charge, capacity));
    private static DeliveryRequest Order(string id = "O01", string zone = "North", decimal distance = 10m, decimal weight = 2m)
        => Ok(DeliveryRequest.Create(id, zone, distance, weight));
    // An EnergyContext can only be obtained by passing the first two pipeline stages.
    private static EnergyContext Energy() => Ok(DeliveryPipeline.ValidateStage(Drone(), Order()).Bind(DeliveryPipeline.EnergyStage));
    private static FleetSnapshot Fleet() => Ok(FleetSnapshot.Create(new[] { Drone(), Drone("D02"), Drone("D03", "South", 60m, 3m) }));
    private sealed class StubNative : INativeSurcharge
    {
        public int Calls { get; private set; }
        public bool Fail { get; set; }
        public Result<decimal> Calculate(decimal price, decimal distance)
        {
            Calls++;
            return Fail ? Result<decimal>.Failure(new PlannerError.NativeFailure("controlled")) : Result<decimal>.Success(distance * 3m);
        }
    }

    private static void DroneValid() => Check(Drone().Charge == 95m, "factory should return valid immutable drone");
    private static void ChargeRejected()
    {
        Check(Error(DronePlanner.Core.Drone.Create("D", "N", -1m, 5m)) is PlannerError.InvalidInput, "negative charge");
        Check(Error(DronePlanner.Core.Drone.Create("D", "N", 101m, 5m)) is PlannerError.InvalidInput, "above capacity");
        Check(Ok(DronePlanner.Core.Drone.Create("D", "N", 0m, 5m)).Charge == 0m, "boundary zero charge is valid but cannot deliver");
    }
    private static void RequestBoundaries()
    {
        Check(Error(DeliveryRequest.Create("O", "N", 0m, 1m)) is PlannerError.InvalidInput, "zero distance invalid");
        Check(Error(DeliveryRequest.Create("O", "N", 201m, 1m)) is PlannerError.InvalidInput, "range cap");
        Check(Ok(DeliveryRequest.Create("O", "N", 200m, 1m)).DistanceKm == 200m, "upper boundary");
    }
    private static void EmptyIds()
    {
        Check(Error(DronePlanner.Core.Drone.Create(" ", "N", 80m, 5m)) is PlannerError.InvalidInput, "empty drone id");
        Check(Error(DeliveryRequest.Create(null, "N", 5m, 1m)) is PlannerError.InvalidInput, "null order id");
    }
    private static void InsufficientEnergy()
    {
        var error = Error(DeliveryPipeline.Run(Drone(charge: 29m), Order(distance: 10m), PricingTier.Standard, PricingPolicies.Standard));
        Check(error is PlannerError.InsufficientEnergy, "needs 20 charge with 10 reserve");
    }
    private static void WrongZone() => Check(Error(DeliveryPipeline.ValidateStage(Drone(), Order(zone: "South"))) is PlannerError.Incompatible, "zone check");
    private static void OversizedPayload() => Check(Error(DeliveryPipeline.ValidateStage(Drone(capacity: 3m), Order(weight: 4m))) is PlannerError.Incompatible, "payload check");
    private static void DifferentPolicies()
    {
        var ctx = Energy();
        Check(PricingPolicies.Standard(ctx) == 1500m, "standard price");
        Check(PricingPolicies.Express(ctx) == 2300m, "express price");
        var comparison = Ok(DeliveryWorkflows.Compare(Fleet(), Order(), "D01"));
        Check(comparison.ExpressPrice - comparison.StandardPrice == 800m, "real comparison workflow");
    }
    private static void Currying()
    {
        var custom = PricingPolicies.CurriedDistancePrice(200m)(50m);
        Check(custom(Energy()) == 700m, "partially applied price");
        Check(PricingPolicies.LowRisk(3m) == 7m && PricingPolicies.HighRisk(3m) == 14m, "two configured risk policies");
    }
    private static void MapBindPropagation()
    {
        bool reached = false;
        Result<int> failure = Result<int>.Failure(new PlannerError.InvalidInput("x", "bad"));
        var output = failure.Map(x => { reached = true; return x * 2; })
            .Bind(x => { reached = true; return Result<string>.Success(x.ToString()); });
        Check(output is Result<string>.Fail && !reached, "failure must short circuit both stages");
        Check(Ok(Result<int>.Success(5).Map(x => x + 2).Bind(x => Result<int>.Success(x * 2))) == 14, "success Map then Bind");
    }
    private static void MissingLookup() => Check(Fleet().FindDrone("missing") is Option<DronePlanner.Core.Drone>.None, "explicit None");
    private static void ExtensionsWork()
    {
        var fleet = Fleet();
        Check(fleet.Drones.InZone("North").Count() == 2, "InZone extension");
        Check(Error(fleet.FindDrone("not-here").ToResult("Drone", "not-here")) is PlannerError.Missing, "ToResult extension");
    }
    private static void EmptyReports()
    {
        var empty = Ok(FleetSnapshot.Create(Array.Empty<DronePlanner.Core.Drone>()));
        Check(FleetReports.Availability(empty).Length == 0, "empty fleet report");
        Check(FleetReports.ZonePerformance(empty).Length == 0, "empty zone report");
        Check(FleetReports.Availability(Fleet(), "nonsense").Length == 0, "no matching zone");
    }
    private static void TieBreaker()
    {
        var rows = FleetReports.Availability(Fleet(), "North");
        Check(rows.Length == 2 && rows[0].DroneId == "D01" && rows[1].DroneId == "D02", "equal charge must sort by ID");
    }
    private static void GroupedReport()
    {
        var fee = new StubNative();
        var booked = Ok(DeliveryWorkflows.Book(Fleet(), Order(), "D01", PricingTier.Standard, fee));
        var completed = Ok(DeliveryWorkflows.Complete(booked, "O01"));
        var groups = FleetReports.ZonePerformance(completed);
        var north = groups.Single(x => x.Zone == "North");
        Check(north.DroneCount == 2 && north.Revenue == 1530m && north.TotalCharge == 170m, "GroupBy, Sum, Count, Avg and revenue");
    }
    private static void BookingCompletion()
    {
        var fee = new StubNative();
        var old = Fleet();
        var booked = Ok(DeliveryWorkflows.Book(old, Order(), "D01", PricingTier.Standard, fee));
        Check(fee.Calls == 1, "native called exactly once");
        Check(booked.Drones.Single(d => d.Id == "D01").Status == DroneStatus.Reserved, "reservation transition");
        Check(booked.Plans.Single().TotalCost == 1530m, "surcharge applied to meaningful final price");
        var completed = Ok(DeliveryWorkflows.Complete(booked, "O01"));
        Check(completed.Plans.Single().Status == DeliveryStatus.Completed, "completion transition");
        Check(completed.Drones.Single(d => d.Id == "D01").Charge == 75m, "charge deducted once");
        Check(old.Plans.Count == 0 && old.Drones.Single(d => d.Id == "D01").Charge == 95m, "immutable input preserved");
    }
    private static void CancelRecharge()
    {
        var booked = Ok(DeliveryWorkflows.Book(Fleet(), Order(), "D01", PricingTier.Express, new StubNative()));
        var cancelled = Ok(DeliveryWorkflows.Cancel(booked, "O01"));
        Check(cancelled.Drones.First().Charge == 95m && cancelled.Drones.First().Status == DroneStatus.Available, "cancel releases but does not consume");
        var recharged = Ok(DeliveryWorkflows.Recharge(cancelled, "D01", 20m));
        Check(recharged.Drones.First().Charge == 100m, "recharge cap");
    }
    private static void SafeOrdering()
    {
        var fresh = Fleet();
        Check(Error(DeliveryWorkflows.Complete(fresh, "O01")) is PlannerError.Missing, "cannot complete before booking");
        var booked = Ok(DeliveryWorkflows.Book(fresh, Order(), "D01", PricingTier.Standard, new StubNative()));
        Check(Error(DeliveryWorkflows.Recharge(booked, "D01", 10m)) is PlannerError.InvalidSequence, "cannot recharge reserved drone");
        var done = Ok(DeliveryWorkflows.Complete(booked, "O01"));
        Check(Error(DeliveryWorkflows.Complete(done, "O01")) is PlannerError.InvalidSequence, "cannot consume twice");
        Check(done.Drones.First().Charge == 75m, "failure leaves previous snapshot unmodified");
    }
    private static void NativeFailureAtomic()
    {
        var current = Fleet();
        var native = new StubNative { Fail = true };
        Check(Error(DeliveryWorkflows.Book(current, Order(), "D01", PricingTier.Standard, native)) is PlannerError.NativeFailure, "native failure propagates");
        Check(current.Plans.Count == 0 && current.Drones.First().Status == DroneStatus.Available, "transaction atomic");
    }
    private static void LazyWorkflow()
    {
        var native = new StubNative();
        var stream = DeliveryWorkflows.BatchPlan(Fleet(), new[] { Order(), Order("O02", "South", 5m, 1m) }, PricingTier.Standard, native);
        Check(native.Calls == 0, "no iteration yet");
        var once = stream.ToArray(); // materialisation boundary
        Check(native.Calls == 2, "evaluated once");
        Check(once.Length == 2 && once.All(s => s.Success), "two valid completed batch transitions");
        var firstSnapshot = once[0].Snapshot;
        Check(firstSnapshot.Plans.Count == 1 && once[1].Snapshot.Plans.Count == 2, "distinct immutable snapshots");
        Check(once.Count() == 2 && native.Calls == 2, "reusing snapshot does not re-evaluate workflow");
    }
    private static void NativeSuccess()
    {
        var native = new NativeSurcharge();
        Check(Ok(native.Calculate(100m, 5m)) == 15m, "real C exported call must return 3x distance fee");
        var fleet = Ok(DeliveryWorkflows.Book(Fleet(), Order(), "D01", PricingTier.Standard, native));
        Check(fleet.Plans.Single().TotalCost == 1530m, "real native calculation incorporated into price");
    }
    private static void NativeMissing() => Check(Ok(NativeSurcharge.VerifyMissingLibraryFailure()), "controlled actual native loader failure");
    private static void MissingJson() => Check(Error(JsonStore.LoadFleet(Path.Combine(Path.GetTempPath(), "missing_" + Guid.NewGuid() + ".json"))) is PlannerError.IoFailure { Code: "MissingFile" }, "explicit missing outcome");
    private static void MalformedJson()
    {
        string p = Path.GetTempFileName();
        try { File.WriteAllText(p, "{ unparseable json"); Check(Error(JsonStore.LoadFleet(p)) is PlannerError.IoFailure { Code: "Malformed" }, "malformed detected"); }
        finally { File.Delete(p); }
    }
    private static void InvalidLoadedData()
    {
        string p = Path.GetTempFileName();
        try { File.WriteAllText(p, "{\"drones\":[{\"id\":\"X\",\"zone\":\"North\",\"charge\":-5,\"maxPayloadKg\":3,\"status\":0}],\"plans\":[]}");
            Check(Error(JsonStore.LoadFleet(p)) is PlannerError.IoFailure { Code: "InvalidData" }, "invalid model rejected"); }
        finally { File.Delete(p); }
    }
    private static void SaveAndReload()
    {
        string p = Path.Combine(Path.GetTempPath(), "drone_roundtrip_" + Guid.NewGuid() + ".json");
        try
        {
            var reserved = Ok(DeliveryWorkflows.Book(Fleet(), Order(), "D01", PricingTier.Express, new StubNative()));
            Ok(JsonStore.SaveFleet(p, reserved));
            var restored = Ok(JsonStore.LoadFleet(p));
            Check(restored.Plans.Count == 1 && restored.Plans.Single().Tier == PricingTier.Express, "roundtrip persists plans");
            Check(restored.FindDrone("D01") is Option<DronePlanner.Core.Drone>.Some, "restores drone status coherently");
        }
        finally { if (File.Exists(p)) File.Delete(p); }
    }
    private static void FileDisposed()
    {
        string p = Path.Combine(Path.GetTempPath(), "drone_disposal_" + Guid.NewGuid() + ".json");
        try
        {
            Ok(JsonStore.SaveFleet(p, Fleet()));
            Ok(JsonStore.LoadFleet(p));
            // Especially on Windows, deletion fails if FileStream was not correctly disposed.
            File.Delete(p);
            Check(!File.Exists(p), "stream released successfully");
        }
        finally { if (File.Exists(p)) File.Delete(p); }
    }
    private static void CsvExport()
    {
        string p = Path.Combine(Path.GetTempPath(), "drone_report_" + Guid.NewGuid() + ".csv");
        try { Ok(JsonStore.ExportCsv(p, Fleet())); var body = File.ReadAllText(p);
            Check(body.Contains("North") && body.Contains("Zone,DroneCount"), "actual CSV report data"); }
        finally { if (File.Exists(p)) File.Delete(p); }
    }
    private static void Immutability()
    {
        var source = new List<DronePlanner.Core.Drone> { Drone() };
        var snapshot = Ok(FleetSnapshot.Create(source));
        source.Clear();
        Check(snapshot.Drones.Count == 1, "caller-owned list must not be retained");
        Check(snapshot.Drones is not IList<DronePlanner.Core.Drone> { IsReadOnly: false }, "snapshot collection should not be mutable");
    }
    private static void DuplicateRequest()
    {
        var existing = Ok(DeliveryWorkflows.Book(Fleet(), Order(), "D01", PricingTier.Standard, new StubNative()));
        Check(Error(DeliveryWorkflows.Book(existing, Order(), "D02", PricingTier.Standard, new StubNative())) is PlannerError.InvalidSequence, "no duplicate request ID");
    }
    private static void PricingSkippedAfterFailure()
    {
        int pricingCalls = 0;
        var native = new StubNative();
        var result = DeliveryWorkflows.Book(Fleet(), Order(distance: 100m), "D01", PricingTier.Standard, native,
            ctx => { pricingCalls++; return PricingPolicies.Standard(ctx); });
        Check(Error(result) is PlannerError.InsufficientEnergy && pricingCalls == 0 && native.Calls == 0, "Map/Bind must skip dependent steps");
    }
    private static void EnergyBoundary()
    {
        // 10 km needs 20 units; with the reserve of 10 a charge of exactly 30 is the lowest accepted value.
        var plan = Ok(DeliveryPipeline.Run(Drone(charge: 30m), Order(distance: 10m), PricingTier.Standard, PricingPolicies.Standard));
        Check(plan.RequiredCharge == 20m && plan.BaseCost == 1500m, "exact boundary must be accepted");
        Check(Error(DeliveryPipeline.Run(Drone(charge: 29.99m), Order(distance: 10m), PricingTier.Standard, PricingPolicies.Standard))
            is PlannerError.InsufficientEnergy { Needed: 20m, Available: 19.99m }, "just below the boundary is rejected with exact figures");
    }
    private static void OrderQueries()
    {
        var orders = new[] { Order(), Order("O02", "South", 5m, 1m), Order("O03", "West", 5m, 1m) };
        Check(orders.FindOrder("o02") is Option<DeliveryRequest>.Some { Value.Id: "O02" }, "case-insensitive Some");
        Check(orders.FindOrder("O99") is Option<DeliveryRequest>.None, "explicit None for a missing order");
        Check(Array.Empty<DeliveryRequest>().FindOrder("O01") is Option<DeliveryRequest>.None, "empty input");
        var fleet = Fleet();
        Check(fleet.FindBestDrone(orders[0]) is Option<DronePlanner.Core.Drone>.Some { Value.Id: "D01" }, "equal charge resolved by ID");
        Check(fleet.FindBestDrone(orders[2]) is Option<DronePlanner.Core.Drone>.None, "no drone in the zone");
        Check(fleet.PendingOrders(orders).Count() == 3, "nothing planned yet");
        var booked = Ok(DeliveryWorkflows.Book(fleet, orders[0], "D01", PricingTier.Standard, new StubNative()));
        Check(booked.PendingOrders(orders).Select(o => o.Id).SequenceEqual(new[] { "O02", "O03" }), "planned order is not pending");
        var cancelled = Ok(DeliveryWorkflows.Cancel(booked, "O01"));
        Check(cancelled.PendingOrders(orders).Count() == 3, "cancelled order is pending again");
    }
    private static void RebookAfterCancel()
    {
        var booked = Ok(DeliveryWorkflows.Book(Fleet(), Order(), "D01", PricingTier.Standard, new StubNative()));
        var cancelled = Ok(DeliveryWorkflows.Cancel(booked, "O01"));
        var rebooked = Ok(DeliveryWorkflows.Book(cancelled, Order(), "D02", PricingTier.Express, new StubNative()));
        var plan = rebooked.Plans.Single();
        Check(plan.Status == DeliveryStatus.Planned && plan.DroneId == "D02" && plan.Tier == PricingTier.Express, "new plan replaces the cancelled one");
        Check(rebooked.Drones.Single(d => d.Id == "D01").Status == DroneStatus.Available
            && rebooked.Drones.Single(d => d.Id == "D02").Status == DroneStatus.Reserved, "only the new drone is reserved");
        Check(cancelled.Plans.Single().Status == DeliveryStatus.Cancelled, "previous snapshot unchanged");
        Check(Error(DeliveryWorkflows.Book(rebooked, Order(), "D01", PricingTier.Standard, new StubNative()))
            is PlannerError.InvalidSequence { Current: "Planned" }, "planned order cannot be booked twice");
        var completed = Ok(DeliveryWorkflows.Complete(rebooked, "O01"));
        Check(Error(DeliveryWorkflows.Book(completed, Order(), "D01", PricingTier.Standard, new StubNative()))
            is PlannerError.InvalidSequence { Current: "Completed" }, "completed order cannot be booked again");
        Check(Error(DeliveryWorkflows.Cancel(completed, "O01")) is PlannerError.InvalidSequence, "completed order cannot be cancelled");
    }
    private static void StateTypesSealed()
    {
        // No public constructors: outside the core these values can only come from the previous stage or a factory.
        foreach (var type in new[] { typeof(ValidatedContext), typeof(EnergyContext), typeof(DeliveryPlan),
                     typeof(DronePlanner.Core.Drone), typeof(DeliveryRequest), typeof(FleetSnapshot) })
            Check(type.GetConstructors().Length == 0, type.Name + " must not expose a public constructor");
    }
    private static void NativeInputRejection() => Check(Error(new NativeSurcharge().Calculate(-1m, 1m)) is PlannerError.InvalidInput, "invalid native parameters rejected in managed boundary");
}
