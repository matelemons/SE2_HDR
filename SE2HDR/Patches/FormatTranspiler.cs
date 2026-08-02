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

    // limit caps how many constants are rewritten, for methods where only the first one is ours.
    // includeUnorm also matches the non-sRGB source format.
    public static IEnumerable<CodeInstruction> ReplaceFormats(
        IEnumerable<CodeInstruction> instructions,
        MethodBase original,
        int limit = int.MaxValue,
        bool includeUnorm = false)
    {
        var codes = new List<CodeInstruction>(instructions);
        var patched = 0;

        foreach (var code in codes)
        {
            if (patched >= limit)
                break;

            if (code.opcode != OpCodes.Ldc_I4_S || code.operand is not sbyte format)
                continue;

            if (format != Source && !(includeUnorm && format == SourceUnorm))
                continue;

            code.operand = Hdr;
            patched++;
        }

        Log.Default.WriteLine(
            $"[{Plugin.Name}] {original?.DeclaringType?.Name}.{original?.Name}: {patched} format reference(s) patched");

        return codes;
    }
}
