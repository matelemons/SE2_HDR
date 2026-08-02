using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace SE2HDR.Patches;

[HarmonyPatch]
public static class BufferInitializePatch
{
    static MethodBase TargetMethod() => PatchTargets.Method(PatchTargets.ScreenBuffers, "InitializeBuffers");

    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original) =>
        FormatTranspiler.ReplaceFormats(instructions, original);
}

[HarmonyPatch]
public static class BufferPlaceholderPatch
{
    static MethodBase TargetMethod() => PatchTargets.Method(PatchTargets.ScreenBuffers, "CreateBackbufferPlaceholder");

    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original) =>
        FormatTranspiler.ReplaceFormats(instructions, original);
}
