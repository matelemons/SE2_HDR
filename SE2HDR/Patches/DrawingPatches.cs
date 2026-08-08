using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SE2HDR.Tools;

namespace SE2HDR.Patches;

[HarmonyPatch]
public static class PsoFormatArrayPatch
{
    static bool Prepare() => RenderMode.Hdr;

    // InitializeAsync is an async method, so we need to patch the compiler-generated
    // state machine's MoveNext rather than the method itself.
    static MethodBase TargetMethod()
    {
        var stateMachineType = PatchTargets.Type(PatchTargets.SpriteRenderer)
                                   .GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public)
                                   .FirstOrDefault(t => t.Name.Contains("InitializeAsync"))
                               ?? throw new InvalidOperationException(
                                   "SpriteRenderer.InitializeAsync state machine not found");

        return AccessTools.Method(stateMachineType, "MoveNext")
               ?? throw new InvalidOperationException("SpriteRenderer.InitializeAsync MoveNext not found");
    }

    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original) =>
        FormatTranspiler.ReplaceFormats(instructions, original, expected: 1);
}

[HarmonyPatch]
public static class ScreenshotConstructorPatch
{
    static bool Prepare() => RenderMode.Hdr;

    static MethodBase TargetMethod() =>
        PatchTargets.Constructor(PatchTargets.ScreenshotsManager,
            typeof(List<Keen.VRage.Library.Threading.Task>));

    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original) =>
        FormatTranspiler.ReplaceFormats(instructions, original, expected: 1);
}

[HarmonyPatch]
public static class ScreenshotTakePatch
{
    static bool Prepare() => RenderMode.Hdr;

    // TakeRequestedScreenshots is generic and each instantiation gets its own IL, so both
    // texture types the game uses have to be patched separately.
    static IEnumerable<MethodBase> TargetMethods()
    {
        var method = (MethodInfo)PatchTargets.Method(PatchTargets.ScreenshotsManager, "TakeRequestedScreenshots");

        return
        [
            method.MakeGenericMethod(
                PatchTargets.Type("Keen.VRage.Render12.Resources.BindableTextures.RenderTargetTexture")),
            method.MakeGenericMethod(
                PatchTargets.Type("Keen.VRage.Render12.Resources.BindableTextures.ResizableRWRenderTargetTexture"))
        ];
    }

    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original) =>
        FormatTranspiler.ReplaceFormats(instructions, original, expected: 1);
}

[HarmonyPatch]
public static class SceneDrawConstructorPatch
{
    static bool Prepare() => RenderMode.Hdr;

    static MethodBase TargetMethod() => PatchTargets.Constructor(PatchTargets.SceneDrawSystem);

    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original) =>
        FormatTranspiler.ReplaceFormats(instructions, original, expected: 1);
}
// Allocates TempLDRBuffer, which receives the tonemapper output when the frame is
// upscaled without FSR.
[HarmonyPatch]
public static class UpscaleTargetFsrPatch
{
    static bool Prepare() => RenderMode.Hdr;

    static MethodBase TargetMethod() => PatchTargets.Method(PatchTargets.SceneDrawSystem, "UpscaleTargetFSR");

    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original) =>
        FormatTranspiler.ReplaceFormats(instructions, original, expected: 2);
}

// Allocates TempOutputLDR, which FXAA renders into before it is copied back.
[HarmonyPatch]
public static class ApplyNonFsrUpscalingAndAaPatch
{
    static bool Prepare() => RenderMode.Hdr;

    static MethodBase TargetMethod() =>
        PatchTargets.Method(PatchTargets.SceneDrawSystem, "ApplyNonFSRUpscalingAndAA");

    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original) =>
        FormatTranspiler.ReplaceFormats(instructions, original, expected: 1);
}

// Allocates the save game thumbnail target and CopyTextureSubresource's the final buffer into it.
// TODO: Tonemap the screenshot?
[HarmonyPatch]
public static class SaveScreenshotPatch
{
    static bool Prepare() => RenderMode.Hdr;

    static MethodBase TargetMethod() => PatchTargets.Method(PatchTargets.SceneDrawSystem, "SaveScreenshot");

    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original) =>
        FormatTranspiler.ReplaceFormats(instructions, original, expected: 1);
}
