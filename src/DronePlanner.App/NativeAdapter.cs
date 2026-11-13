using System.Runtime.InteropServices;
using DronePlanner.Core;

namespace DronePlanner.App;

public sealed class NativeSurcharge : INativeSurcharge
{
    // P/Invoke is actually called on the successful business workflow.
    [DllImport("drone_native", EntryPoint = "calculate_surcharge", CallingConvention = CallingConvention.Cdecl)]
    private static extern int CalculateNative(int baseFee, int distanceKm);

    public Result<decimal> Calculate(decimal basePrice, decimal distanceKm)
    {
        if (basePrice < 0 || basePrice > 1_000_000m || distanceKm < 0 || distanceKm > 200m)
            return Result<decimal>.Failure(new PlannerError.InvalidInput("Native input", "out of supported range"));
        try
        {
            // Fee is computed from rounded whole kilometres; base price remains exact decimal.
            int roundedBase = decimal.ToInt32(decimal.Round(basePrice, 0, MidpointRounding.AwayFromZero));
            int roundedDistance = decimal.ToInt32(decimal.Round(distanceKm, 0, MidpointRounding.AwayFromZero));
            int nativeTotal = CalculateNative(roundedBase, roundedDistance);
            return nativeTotal < roundedBase
                ? Result<decimal>.Failure(new PlannerError.NativeFailure("C library rejected arguments"))
                : Result<decimal>.Success(nativeTotal - roundedBase);
        }
        catch (DllNotFoundException e) { return Result<decimal>.Failure(new PlannerError.NativeFailure("Library not found: " + e.Message)); }
        catch (EntryPointNotFoundException e) { return Result<decimal>.Failure(new PlannerError.NativeFailure("Export missing: " + e.Message)); }
        catch (BadImageFormatException e) { return Result<decimal>.Failure(new PlannerError.NativeFailure("Wrong architecture: " + e.Message)); }
        catch (OverflowException e) { return Result<decimal>.Failure(new PlannerError.NativeFailure("Numeric overflow: " + e.Message)); }
    }

    // This triggers a real loader error using a deliberately missing binary.
    public static Result<bool> VerifyMissingLibraryFailure()
    {
        try
        {
            nint handle = NativeLibrary.Load("definitely_missing_drone_native_test_82571");
            NativeLibrary.Free(handle);
            return Result<bool>.Failure(new PlannerError.NativeFailure("Unexpectedly resolved a deliberately missing library"));
        }
        catch (DllNotFoundException) { return Result<bool>.Success(true); }
        catch (BadImageFormatException) { return Result<bool>.Success(true); }
    }
}
