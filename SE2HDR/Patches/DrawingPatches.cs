using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace SE2HDR.Patches;

[HarmonyPatch]
public static class PsoFormatArrayPatch
{
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
        FormatTranspiler.ReplaceFormats(instructions, original);
}

[HarmonyPatch]
public static class ScreenshotConstructorPatch
{
    static MethodBase TargetMethod() =>
        PatchTargets.Constructor(PatchTargets.ScreenshotsManager,
            typeof(List<Keen.VRage.Library.Threading.Task>));

    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original) =>
        FormatTranspiler.ReplaceFormats(instructions, original, limit: 1);
}

[HarmonyPatch]
public static class ScreenshotTakePatch
{
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
        FormatTranspiler.ReplaceFormats(instructions, original);
}

[HarmonyPatch]
public static class SceneDrawConstructorPatch
{
    static MethodBase TargetMethod() => PatchTargets.Constructor(PatchTargets.SceneDrawSystem);

    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original) =>
        FormatTranspiler.ReplaceFormats(instructions, original);
}

[HarmonyPatch]
public static class ExecutePostPassesPatch
{
    static MethodBase TargetMethod() => PatchTargets.Method(PatchTargets.SceneDrawSystem, "ExecutePostPasses");

    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original) =>
        FormatTranspiler.ReplaceFormats(instructions, original, includeUnorm: true);
}
