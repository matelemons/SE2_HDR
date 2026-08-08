using Keen.VRage.Library.Diagnostics;

namespace SE2HDR.Tools;

// Decides whether the plugin patches an HDR10 swapchain or leaves the game on
// its SDR output and only replaces the tonemapping curve.
internal static class RenderMode
{
    // Peak luminance to fall back on when the display reports nothing usable.
    public const int DefaultPeakNits = 1000;

    public static bool Hdr { get; private set; }

    // What the display reported, or 0 when it reported nothing.
    public static int DetectedPeakNits { get; private set; }

    public static string StatusText { get; private set; } = "The display has not been checked yet.";

    public static void Resolve()
    {
        var display = DisplayProbe.Probe();
        DetectedPeakNits = display.PeakNits;

        string reason;
        switch (Config.Current.OutputMode)
        {
            case OutputMode.ForceHdr:
                Hdr = true;
                reason = "HDR output is forced in the plugin settings";
                break;

            case OutputMode.ForceSdr:
                Hdr = false;
                reason = "SDR output is forced in the plugin settings";
                break;

            default:
                Hdr = display.IsHdr;
                reason = !display.Detected
                    ? "no display could be detected"
                    : display.IsHdr
                        ? "the display is in HDR mode"
                        : "the display is not in HDR mode";
                break;
        }

        StatusText = $"Running in {(Hdr ? "HDR" : "SDR")} mode, because {reason}.";

        // Only shown while the display is in HDR mode
        if (display.Details != null)
            StatusText += "\n" + display.Details;

        foreach (var line in StatusText.Split('\n'))
            Log.Default.WriteLine($"[{Plugin.Name}] {line}");

        if (!Hdr && display.IsHdr)
            Log.Default.WriteLine(LogSeverity.Warning,
                $"[{Plugin.Name}] An HDR display was detected but SDR output was requested.");
    }
}
