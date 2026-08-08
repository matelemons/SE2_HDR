using System;
using Keen.VRage.Library.Diagnostics;
using Vortice.DXGI;

namespace SE2HDR.Tools;

internal readonly struct DisplayInfo
{
    public bool Detected { get; init; }
    public bool IsHdr { get; init; }
    
    public int PeakNits { get; init; }
    public string Details { get; init; }
}

internal static class DisplayProbe
{
    private const ColorSpaceType Hdr10 = ColorSpaceType.RgbFullG2084NoneP2020;

    public static DisplayInfo Probe()
    {
        try
        {
            return ProbeOutputs();
        }
        catch (Exception ex)
        {
            Log.Default.WriteLine(LogSeverity.Warning, $"[{Plugin.Name}] Display probe failed: {ex.Message}");
            return new DisplayInfo();
        }
    }

    private static DisplayInfo ProbeOutputs()
    {
        using var factory = DXGI.CreateDXGIFactory1<IDXGIFactory1>();

        var firstFound = new DisplayInfo();
        var haveAny = false;

        for (uint adapterIndex = 0;
             factory.EnumAdapters1(adapterIndex, out var adapter).Success && adapter != null;
             adapterIndex++)
        {
            using (adapter)
            {
                for (uint outputIndex = 0;
                     adapter.EnumOutputs(outputIndex, out var output).Success && output != null;
                     outputIndex++)
                {
                    using (output)
                    {
                        if (!TryDescribe(output, out var info, out var isPrimary))
                            continue;

                        if (isPrimary)
                            return info;

                        if (haveAny)
                            continue;

                        firstFound = info;
                        haveAny = true;
                    }
                }
            }
        }

        return firstFound;
    }

    private static bool TryDescribe(IDXGIOutput output, out DisplayInfo info, out bool isPrimary)
    {
        info = default;
        isPrimary = false;

        IDXGIOutput6 output6;
        try
        {
            output6 = output.QueryInterface<IDXGIOutput6>();
        }
        catch (Exception)
        {
            // A DXGI runtime without the HDR interfaces cannot be in HDR mode either.
            return false;
        }

        using (output6)
        {
            var description = output6.Description1;
            var isHdr = description.ColorSpace == Hdr10;

            isPrimary = description.DesktopCoordinates.Left == 0 && description.DesktopCoordinates.Top == 0;
            info = new DisplayInfo
            {
                Detected = true,
                IsHdr = isHdr,
                PeakNits = isHdr ? (int)MathF.Round(description.MaxLuminance) : 0,
                Details = isHdr
                    ? $"{description.DeviceName}: {description.BitsPerColor}-bit, "
                      + $"peak {description.MaxLuminance:F0} nits, "
                      + $"full frame {description.MaxFullFrameLuminance:F0} nits, "
                      + $"black {description.MinLuminance:F3} nits"
                    : null,
            };
            return true;
        }
    }
}
