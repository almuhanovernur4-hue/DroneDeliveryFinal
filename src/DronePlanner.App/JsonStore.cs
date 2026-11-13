using System.Text.Json;
using DronePlanner.Core;

namespace DronePlanner.App;

public sealed record DroneDto(string? Id, string? Zone, decimal Charge, decimal MaxPayloadKg, DroneStatus Status);
public sealed record PlanDto(string? RequestId, string? DroneId, string? Zone, decimal DistanceKm,
    decimal RequiredCharge, PricingTier Tier, decimal BaseCost, decimal NativeSurcharge, DeliveryStatus Status);
public sealed record FleetDto(DroneDto[]? Drones, PlanDto[]? Plans);
public sealed record RequestDto(string? Id, string? Zone, decimal DistanceKm, decimal WeightKg);

public static class JsonStore
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true, WriteIndented = true };

    public static Result<FleetSnapshot> LoadFleet(string filename)
    {
        try
        {
            using var stream = File.OpenRead(filename);
            var raw = JsonSerializer.Deserialize<FleetDto>(stream, Options);
            if (raw?.Drones is null || raw.Plans is null)
                return Result<FleetSnapshot>.Failure(new PlannerError.IoFailure("Malformed", "Fleet JSON requires drones and plans arrays"));
            var ds = new List<Drone>();
            foreach (var dto in raw.Drones)
            {
                if (dto is null) return Result<FleetSnapshot>.Failure(new PlannerError.IoFailure("Malformed", "Null drone"));
                if (Drone.Create(dto.Id, dto.Zone, dto.Charge, dto.MaxPayloadKg, dto.Status) is not Result<Drone>.Ok valid)
                    return Result<FleetSnapshot>.Failure(new PlannerError.IoFailure("InvalidData", $"Invalid drone {dto.Id}"));
                ds.Add(valid.Value);
            }
            var ps = new List<DeliveryPlan>();
            foreach (var dto in raw.Plans)
            {
                if (dto is null) return Result<FleetSnapshot>.Failure(new PlannerError.IoFailure("Malformed", "Null delivery plan"));
                var restored = DeliveryPlan.Restore(dto.RequestId, dto.DroneId, dto.Zone, dto.DistanceKm,
                    dto.RequiredCharge, dto.Tier, dto.BaseCost, dto.NativeSurcharge, dto.Status);
                if (restored is not Result<DeliveryPlan>.Ok valid)
                    return Result<FleetSnapshot>.Failure(new PlannerError.IoFailure("InvalidData", $"Invalid plan {dto.RequestId}"));
                ps.Add(valid.Value);
            }
            return FleetSnapshot.Create(ds, ps);
        }
        catch (FileNotFoundException) { return Result<FleetSnapshot>.Failure(new PlannerError.IoFailure("MissingFile", filename)); }
        catch (DirectoryNotFoundException) { return Result<FleetSnapshot>.Failure(new PlannerError.IoFailure("MissingFile", filename)); }
        catch (JsonException e) { return Result<FleetSnapshot>.Failure(new PlannerError.IoFailure("Malformed", e.Message)); }
        catch (IOException e) { return Result<FleetSnapshot>.Failure(new PlannerError.IoFailure("IO", e.Message)); }
        catch (UnauthorizedAccessException e) { return Result<FleetSnapshot>.Failure(new PlannerError.IoFailure("Permission", e.Message)); }
    }

    public static Result<DeliveryRequest[]> LoadOrders(string filename)
    {
        try
        {
            using var stream = File.OpenRead(filename);
            var raw = JsonSerializer.Deserialize<RequestDto[]>(stream, Options);
            if (raw is null) return Result<DeliveryRequest[]>.Failure(new PlannerError.IoFailure("Malformed", "Orders array is required"));
            var orders = new List<DeliveryRequest>();
            foreach (var dto in raw)
            {
                if (dto is null) return Result<DeliveryRequest[]>.Failure(new PlannerError.IoFailure("Malformed", "Null order"));
                if (DeliveryRequest.Create(dto.Id, dto.Zone, dto.DistanceKm, dto.WeightKg) is not Result<DeliveryRequest>.Ok valid)
                    return Result<DeliveryRequest[]>.Failure(new PlannerError.IoFailure("InvalidData", $"Invalid order {dto.Id}"));
                orders.Add(valid.Value);
            }
            if (orders.Select(o => o.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != orders.Count)
                return Result<DeliveryRequest[]>.Failure(new PlannerError.IoFailure("InvalidData", "Duplicate order IDs"));
            return Result<DeliveryRequest[]>.Success(orders.ToArray());
        }
        catch (FileNotFoundException) { return Result<DeliveryRequest[]>.Failure(new PlannerError.IoFailure("MissingFile", filename)); }
        catch (DirectoryNotFoundException) { return Result<DeliveryRequest[]>.Failure(new PlannerError.IoFailure("MissingFile", filename)); }
        catch (JsonException e) { return Result<DeliveryRequest[]>.Failure(new PlannerError.IoFailure("Malformed", e.Message)); }
        catch (IOException e) { return Result<DeliveryRequest[]>.Failure(new PlannerError.IoFailure("IO", e.Message)); }
        catch (UnauthorizedAccessException e) { return Result<DeliveryRequest[]>.Failure(new PlannerError.IoFailure("Permission", e.Message)); }
    }

    public static Result<string> SaveFleet(string filename, FleetSnapshot snapshot)
    {
        try
        {
            string? dir = Path.GetDirectoryName(Path.GetFullPath(filename));
            if (dir is not null) Directory.CreateDirectory(dir);
            // Write to a temp file then move, so serialization failures do not corrupt existing state.
            string tmp = filename + ".tmp";
            try
            {
                using (var stream = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    var dto = new FleetDto(snapshot.Drones.Select(d => new DroneDto(d.Id, d.Zone, d.Charge, d.MaxPayloadKg, d.Status)).ToArray(),
                        snapshot.Plans.Select(p => new PlanDto(p.RequestId,p.DroneId,p.Zone,p.DistanceKm,p.RequiredCharge,
                            p.Tier,p.BaseCost,p.NativeSurcharge,p.Status)).ToArray());
                    JsonSerializer.Serialize(stream, dto, Options);
                }
                File.Move(tmp, filename, true);
            }
            finally { if (File.Exists(tmp)) File.Delete(tmp); }
            return Result<string>.Success(filename);
        }
        catch (IOException e) { return Result<string>.Failure(new PlannerError.IoFailure("IO", e.Message)); }
        catch (UnauthorizedAccessException e) { return Result<string>.Failure(new PlannerError.IoFailure("Permission", e.Message)); }
    }

    public static Result<string> ExportCsv(string filename, FleetSnapshot fleet)
    {
        try
        {
            string? dir = Path.GetDirectoryName(Path.GetFullPath(filename));
            if (dir is not null) Directory.CreateDirectory(dir);
            using var writer = new StreamWriter(filename, false);
            writer.WriteLine("Zone,DroneCount,TotalCharge,AverageCharge,BookedDeliveries,Revenue");
            foreach (var row in FleetReports.ZonePerformance(fleet))
                writer.WriteLine($"{Csv(row.Zone)},{row.DroneCount},{Format(row.TotalCharge)},{Format(row.AverageCharge)},{row.BookedDeliveries},{Format(row.Revenue)}");
            return Result<string>.Success(filename);
        }
        catch (IOException e) { return Result<string>.Failure(new PlannerError.IoFailure("IO", e.Message)); }
        catch (UnauthorizedAccessException e) { return Result<string>.Failure(new PlannerError.IoFailure("Permission", e.Message)); }
    }
    private static string Csv(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
    private static string Format(decimal value) => value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
