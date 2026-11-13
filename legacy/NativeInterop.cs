using System;
using System.Runtime.InteropServices;

public static class NativeInterop
{
    private const string LibName = "native_calc";

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    private static extern int CalculateSurcharge(int baseFee, int distance);

    public static Result<int, string> SafeCalculateSurcharge(int baseFee, int distance)
    {
        if (baseFee < 0 || distance < 0)
            return new Result<int, string>.Fail("Native input validation failed: negative parameters.");

        try
        {
            int result = CalculateSurcharge(baseFee, distance);
            if (result < 0)
                return new Result<int, string>.Fail("Native function returned a domain error code.");

            return new Result<int, string>.Ok(result);
        }
        catch (DllNotFoundException)
        {
            return new Result<int, string>.Fail("Native library binary could not be loaded.");
        }
        catch (Exception ex)
        {
            return new Result<int, string>.Fail($"Native interop error: {ex.Message}");
        }
    }
}