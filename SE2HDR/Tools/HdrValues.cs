using System;

namespace SE2HDR.Tools;

internal static class HdrValues
{
    public static float PeakNits => Config.Current.PeakNits;

    public static float PaperWhiteNits => Config.Current.PaperWhiteNits;

    // Params for the tonemapping shader, compressed into the one spare int at the end of
    // PostProcessSettings. 
    // 
    // SE2HDR_GetSettings() in HdrHlsl.Tonemap unpacks this and must match.
    //
    //   bits  0-11  peak luminance in nits      (0-4095)
    //   bits 12-20  paper white in nits         (0-511)
    //   bits 21-26  oversaturation              (0-63, /63 in the shader)
    //   bit  27     dither
    //   bits 28-30  TonemapMode                 (0-7)
    //   bit  31     unused, always clear
    public static int PackedSettings()
    {
        var config = Config.Current;

        var peak = (uint)Math.Clamp(config.PeakNits, 0, 4095);
        var ui = (uint)Math.Clamp(config.PaperWhiteNits, 0, 511);
        var oversaturation = (uint)Math.Clamp((int)MathF.Round(config.Oversaturation * 63f), 0, 63);
        var dither = config.Dither ? 1u : 0u;
        var mode = (uint)Math.Clamp((int)config.TonemapMode, 0, 7);

        return (int)(peak | (ui << 12) | (oversaturation << 21) | (dither << 27) | (mode << 28));
    }

    private static int appliedPaperWhiteNits = -1;

    // The Slug setup buffer is only rebuilt when the resolution changes, so the renderer
    // needs to be notified when a nits change makes the cached one stale.
    public static bool ConsumePaperWhiteNitsChange()
    {
        var current = Config.Current.PaperWhiteNits;
        if (current == appliedPaperWhiteNits)
            return false;

        appliedPaperWhiteNits = current;
        return true;
    }
}
