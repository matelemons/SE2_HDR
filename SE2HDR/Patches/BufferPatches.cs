using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SE2HDR.Tools;

namespace SE2HDR.Patches;

[HarmonyPatch]
public static class BufferInitializePatch
{
    static bool Prepare() => RenderMode.Hdr;

    static MethodBase TargetMethod() => PatchTargets.Method(PatchTargets.ScreenBuffers, "InitializeBuffers");

    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original) =>
        FormatTranspiler.ReplaceFormats(instructions, original);
}

[HarmonyPatch]
public static class BufferPlaceholderPatch
{
    static bool Prepare() => RenderMode.Hdr;

    static MethodBase TargetMethod() => PatchTargets.Method(PatchTargets.ScreenBuffers, "CreateBackbufferPlaceholder");

    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original) =>
        FormatTranspiler.ReplaceFormats(instructions, original);
}
