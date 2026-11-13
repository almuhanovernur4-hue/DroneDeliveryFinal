using DronePlanner.Core;

namespace DronePlanner.App;

public static class Program
{
    public static int Main(string[] args)
    {
        string dataDir = Path.Combine(AppContext.BaseDirectory, "data");
        for (int i = 0; i < args.Length; i++)
            if (args[i] == "--data-dir" && i + 1 < args.Length) dataDir = args[++i];
        if (args.Contains("--help") || args.Contains("-h"))
        {
            Console.WriteLine("Drone Delivery Planner — simple console simulation");
            Console.WriteLine("Run normally for the menu, or add --demo for a quick defense example.");
            Console.WriteLine("Use --data-dir <folder> to load your own fleet.json and orders.json.");
            return 0;
        }
        var loaded = JsonStore.LoadFleet(Path.Combine(dataDir, "fleet.json"));
        var orderLoad = JsonStore.LoadOrders(Path.Combine(dataDir, "orders.json"));
        if (loaded is not Result<FleetSnapshot>.Ok fleetOk) { PrintFailure(loaded); return 1; }
        if (orderLoad is not Result<DeliveryRequest[]>.Ok ordersOk) { PrintFailure(orderLoad); return 1; }
        FleetSnapshot fleet = fleetOk.Value;
        DeliveryRequest[] orders = ordersOk.Value;
        INativeSurcharge native = new NativeSurcharge();
        if (args.Contains("--demo")) { Demo(fleet, orders, native); return 0; }
        Console.WriteLine("=== DRONE DELIVERY PLANNER ===");
        Console.WriteLine($"Loaded {fleet.Drones.Count} drones and {orders.Length} sample orders.");
        Console.WriteLine("Tip: choose 9 for a ready-made defense demo without changing your work.");
        while (true)
        {
            Console.WriteLine("\n 1 - Book one order       2 - Auto-book remaining orders");
            Console.WriteLine(" 3 - Complete an order    4 - Recharge an idle drone");
            Console.WriteLine(" 5 - Cancel a booking     6 - Two reports");
            Console.WriteLine(" 7 - Compare two prices   8 - Save fleet + export CSV");
            Console.WriteLine(" 9 - Quick defense demo   0 - Exit");
            Console.Write("Choice: ");
            string? choice = Console.ReadLine();
            if (choice is null || choice == "0") return 0;
            switch (choice)
            {
                case "1":
                {
                    string orderId = Ask("Order ID: ");
                    var found = orders.FindOrder(orderId).ToResult("Order", orderId);
                    if (found is not Result<DeliveryRequest>.Ok order) { PrintFailure(found); break; }
                    string droneId = Ask("Drone ID: ");
                    PricingTier tier = Ask("Express? (y/n): ").Equals("y", StringComparison.OrdinalIgnoreCase)
                        ? PricingTier.Express : PricingTier.Standard;
                    Replace(DeliveryWorkflows.Book(fleet, order.Value, droneId, tier, native), ref fleet);
                    break;
                }
                case "2":
                {
                    // Both the pending filter and BatchPlan are deferred; nothing runs until ToArray().
                    // Single deliberate materialisation: the iterator is evaluated exactly once.
                    BatchStep[] steps = DeliveryWorkflows.BatchPlan(fleet, fleet.PendingOrders(orders), PricingTier.Standard, native).ToArray();
                    foreach (var s in steps) Console.WriteLine($"{s.RequestId}: {(s.Success ? "SUCCESS" : "FAILED")} {s.Message}");
                    if (steps.Length > 0) fleet = steps[^1].Snapshot;
                    break;
                }
                case "3": Replace(DeliveryWorkflows.Complete(fleet, Ask("Order ID: ")), ref fleet); break;
                case "4":
                {
                    string droneId = Ask("Drone ID: ");
                    if (decimal.TryParse(Ask("Recharge amount: ").Replace(',', '.'), System.Globalization.NumberStyles.Number,
                            System.Globalization.CultureInfo.InvariantCulture, out decimal charge))
                        Replace(DeliveryWorkflows.Recharge(fleet, droneId, charge), ref fleet);
                    else Console.WriteLine("Enter a valid numeric value");
                    break;
                }
                case "5": Replace(DeliveryWorkflows.Cancel(fleet, Ask("Order ID: ")), ref fleet); break;
                case "6":
                {
                    string zone = Ask("Zone filter for the availability report (Enter = all zones): ");
                    PrintReports(fleet, zone.Length == 0 ? null : zone);
                    break;
                }
                case "7":
                {
                    string orderId = Ask("Order ID: ");
                    var found = orders.FindOrder(orderId).ToResult("Order", orderId);
                    if (found is not Result<DeliveryRequest>.Ok order) { PrintFailure(found); break; }
                    var compare = DeliveryWorkflows.Compare(fleet, order.Value, Ask("Drone ID: "));
                    if (compare is Result<PriceComparison>.Ok ok)
                        Console.WriteLine($"Standard {ok.Value.StandardPrice}; Express {ok.Value.ExpressPrice}; difference {ok.Value.ExpressPrice - ok.Value.StandardPrice}");
                    else PrintFailure(compare);
                    break;
                }
                case "9":
                    Demo(LoadFreshDemoFleet(dataDir, fleet), orders, native);
                    break;
                case "8":
                {
                    string outputDir = Path.Combine(dataDir, "output");
                    PrintSave(JsonStore.SaveFleet(Path.Combine(outputDir, "fleet-updated.json"), fleet));
                    PrintSave(JsonStore.ExportCsv(Path.Combine(outputDir, "zone-report.csv"), fleet));
                    break;
                }
                default: Console.WriteLine("Unknown selection"); break;
            }
        }
    }

    // The demo is a separate snapshot. A student's interactive changes stay untouched.
    private static FleetSnapshot LoadFreshDemoFleet(string dataDir, FleetSnapshot fallback)
        => JsonStore.LoadFleet(Path.Combine(dataDir, "fleet.json")) is Result<FleetSnapshot>.Ok ok ? ok.Value : fallback;

    private static string Ask(string prompt) { Console.Write(prompt); return Console.ReadLine()?.Trim() ?? ""; }
    private static void Replace(Result<FleetSnapshot> outcome, ref FleetSnapshot fleet)
    {
        if (outcome is Result<FleetSnapshot>.Ok ok) { fleet = ok.Value; Console.WriteLine("SUCCESS (immutable state transition)"); }
        else PrintFailure(outcome);
    }
    private static void PrintFailure<T>(Result<T> result)
    {
        if (result is Result<T>.Fail fail) Console.WriteLine("EXPECTED/CONTROLLED FAILURE: " + fail.Error.Describe());
    }
    private static void PrintSave(Result<string> result)
    {
        if (result is Result<string>.Ok ok) Console.WriteLine("Saved: " + ok.Value);
        else PrintFailure(result);
    }
    private static void PrintReports(FleetSnapshot fleet, string? zone = null)
    {
        Console.WriteLine("--- Fleet availability (charge descending, drone ID tie-breaker) ---");
        FleetRow[] rows = FleetReports.Availability(fleet, zone);
        if (rows.Length == 0) Console.WriteLine("(no drones match)");
        foreach (var r in rows)
            Console.WriteLine($"{r.DroneId,-4} {r.Zone,-6} {r.Charge,6:0.0} {r.Status,-9} risk={r.RiskIndex:0.0}");
        Console.WriteLine("--- Zone utilisation, aggregation and completed revenue ---");
        foreach (var z in FleetReports.ZonePerformance(fleet))
            Console.WriteLine($"{z.Zone,-6} count={z.DroneCount} sum-charge={z.TotalCharge} avg={z.AverageCharge:0.0} booked={z.BookedDeliveries} revenue={z.Revenue}");
    }
    private static void Demo(FleetSnapshot fleet, IReadOnlyList<DeliveryRequest> orders, INativeSurcharge native)
    {
        Console.WriteLine("=== LIVE DEFENSE DEMO (sample O01 / D01) ===");
        if (orders.Count == 0) { Console.WriteLine("No orders in the input data for demo"); return; }
        var order = orders[0];
        var comparison = DeliveryWorkflows.Compare(fleet, order, "D01");
        if (comparison is Result<PriceComparison>.Ok cp)
            Console.WriteLine($"PRICING: standard {cp.Value.StandardPrice} vs express {cp.Value.ExpressPrice}");
        var booked = DeliveryWorkflows.Book(fleet, order, "D01", PricingTier.Standard, native);
        if (booked is not Result<FleetSnapshot>.Ok ok) { PrintFailure(booked); return; }
        fleet = ok.Value;
        var plan = fleet.Plans.Single(p => p.RequestId == order.Id);
        Console.WriteLine($"BOOKED: order={plan.RequestId}, drone={plan.DroneId}, base={plan.BaseCost}, native={plan.NativeSurcharge}, total={plan.TotalCost}");
        PrintFailure(DeliveryWorkflows.Book(fleet, order, "D01", PricingTier.Standard, native));
        PrintFailure(DeliveryWorkflows.Recharge(fleet, "D01", 20m));
        var completed = DeliveryWorkflows.Complete(fleet, order.Id);
        if (completed is Result<FleetSnapshot>.Ok co) { fleet = co.Value; Console.WriteLine("COMPLETED: energy deducted once"); }
        PrintFailure(DeliveryWorkflows.Complete(fleet, order.Id));
        var recharged = DeliveryWorkflows.Recharge(fleet, "D01", 20m);
        if (recharged is Result<FleetSnapshot>.Ok re) { fleet = re.Value; Console.WriteLine("RECHARGED: idle drone"); }
        // Deferred execution made visible: the counter only moves when the iterator is consumed.
        int pulled = 0;
        IEnumerable<DeliveryRequest> remaining = fleet.PendingOrders(orders.Skip(1)).Select(o => { pulled++; return o; });
        IEnumerable<BatchStep> lazyBatch = DeliveryWorkflows.BatchPlan(fleet, remaining, PricingTier.Standard, native);
        Console.WriteLine($"LAZY: batch iterator created, orders processed so far: {pulled}");
        BatchStep[] steps = lazyBatch.ToArray(); // deliberate materialisation boundary
        Console.WriteLine($"LAZY: after ToArray(), orders processed: {pulled}");
        Console.WriteLine($"BATCH: {steps.Count(s => s.Success)} accepted / {steps.Count(s => !s.Success)} rejected");
        foreach (var step in steps) Console.WriteLine($"  {step.RequestId}: {step.Message}");
        if (steps.Length > 0) fleet = steps[^1].Snapshot;
        var cancellable = fleet.Plans.FirstOrDefault(p => p.Status == DeliveryStatus.Planned);
        if (cancellable is not null)
        {
            var cancelled = DeliveryWorkflows.Cancel(fleet, cancellable.RequestId);
            if (cancelled is Result<FleetSnapshot>.Ok c) { fleet = c.Value; Console.WriteLine("CANCELLED: reserved drone released"); }
        }
        PrintReports(fleet);
        Console.WriteLine("Run the automated suite: dotnet run --project tests/DronePlanner.Tests");
    }
}
