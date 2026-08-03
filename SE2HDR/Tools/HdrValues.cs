namespace SE2HDR.Tools;

internal static class HdrValues
{
    public static float PeakNits => Config.Current.PeakNits;

    public static float UiNits => Config.Current.UiNits;

    private static int appliedUiNits = -1;

    // The Slug setup buffer is only rebuilt when the resolution changes, so the renderer
    // needs to be notified when a nits change makes the cached one stale.
    public static bool ConsumeUiNitsChange()
    {
        var current = Config.Current.UiNits;
        if (current == appliedUiNits)
            return false;

        appliedUiNits = current;
        return true;
    }
}
