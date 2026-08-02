using HarmonyLib;
using Keen.VRage.Core.Render;
using Keen.VRage.Library.Diagnostics;
using Keen.VRage.Library.Mathematics;
using System;
using System.Collections.Generic;
using System.Reflection;
using Vortice.DXGI;

namespace SE2HDR.Patches;

[HarmonyPatch]
public static class SwapchainPatch
{
    static MethodBase TargetMethod() => PatchTargets.Method(PatchTargets.SwapChain, "CreateD3DSwapChain");

    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original) =>
        FormatTranspiler.ReplaceFormats(instructions, original, limit: 1);

    static void Postfix(ref IDXGISwapChain3 __result)
    {
        if (__result == null)
        {
            Log.Default.WriteLine($"[{Plugin.Name}] ERROR: Swapchain creation returned null!");
            return;
        }

        try
        {
            // Set HDR10 color space (BT.2020 primaries, ST.2084 EOTF/PQ curve)
            var colorSpace = ColorSpaceType.RgbFullG2084NoneP2020;

            try
            {
                __result.SetColorSpace1(colorSpace);
            }
            catch (Exception e)
            {
                Log.Default.WriteLine($"[{Plugin.Name}] WARNING: SetColorSpace1 failed: " + e);
                Log.Default.WriteLine(
                    $"[{Plugin.Name}] This might mean your display doesn't support HDR, or HDR is not enabled in Windows settings");
            }
        }
        catch (Exception ex)
        {
            Log.Default.WriteLine($"[{Plugin.Name}] ERROR setting color space: {ex.Message}");
        }
    }
}

[HarmonyPatch]
public static class SwapchainResizePatch
{
    static MethodBase TargetMethod() =>
        PatchTargets.Method(PatchTargets.SwapChain, "Resize", typeof(Vector2I));

    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original) =>
        FormatTranspiler.ReplaceFormats(instructions, original);
}

[HarmonyPatch]
public static class SwapchainInitializeBackBufferPatch
{
    static MethodBase TargetMethod() =>
        PatchTargets.Method(PatchTargets.SwapChain, "InitializeBackBufferWrappers");

    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original) =>
        FormatTranspiler.ReplaceFormats(instructions, original);
}

[HarmonyPatch]
public static class SwapchainConstructorPatch
{
    static MethodBase TargetMethod() =>
        PatchTargets.Constructor(PatchTargets.SwapChain,
            typeof(RenderDisplaySettings).MakeByRefType(),
            PatchTargets.Type("Keen.VRage.Core.Platform.IPlatformWindows"));

    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original) =>
        FormatTranspiler.ReplaceFormats(instructions, original);
}

// Previously, SwapChain.Update was also patched. This is not done anymore as
// the original code was modified to add an exception filter which Harmony cannot rebuild.

// In either case, it doesn't actually affect anything, even if it does reference the SDR format. As far
// as I understand, this only performs memory safety tracking, but both old and new formats are 32bpp.
