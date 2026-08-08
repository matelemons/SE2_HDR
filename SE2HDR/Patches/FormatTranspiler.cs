using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Keen.VRage.Library.Diagnostics;

namespace SE2HDR.Patches;

// The engine bakes buffer formats into IL as constants.
// Find those constants and swap for the HDR format.
internal static class FormatTranspiler
{
    private const sbyte Hdr = (sbyte)Plugin.HDR_FORMAT;
    private const sbyte Source = (sbyte)Plugin.SOURCE_FORMAT;
    private const sbyte SourceUnorm = (sbyte)Plugin.SOURCE_FORMAT_UNORM;

    // expected is how many constants the method is known to hold. A mismatch means the game moved
    // them elsewhere, which would leave buffers half-converted, so refuse instead of patching.
    // includeUnorm also matches the non-sRGB source format.
    public static IEnumerable<CodeInstruction> ReplaceFormats(
        IEnumerable<CodeInstruction> instructions,
        MethodBase original,
        int expected,
        bool includeUnorm = false)
    {
        var codes = new List<CodeInstruction>(instructions);
        var patched = 0;

        foreach (var code in codes)
        {
            if (code.opcode != OpCodes.Ldc_I4_S || code.operand is not sbyte format)
                continue;

            if (format != Source && !(includeUnorm && format == SourceUnorm))
                continue;

            code.operand = Hdr;
            patched++;
        }

        var name = $"{original?.DeclaringType?.Name}.{original?.Name}";

        if (patched != expected)
            throw new InvalidOperationException(
                $"{name} holds {patched} format reference(s), expected {expected}");

        Log.Default.WriteLine($"[{Plugin.Name}] {name}: {patched} format reference(s) patched");

        return codes;
    }
}
