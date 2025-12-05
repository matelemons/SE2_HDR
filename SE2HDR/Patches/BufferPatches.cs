using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace SE2HDR.Patches
{
    [HarmonyPatch]
    public static class BufferInitializePatch
    {
        static MethodBase TargetMethod()
        {
            var type = AccessTools.TypeByName("Keen.VRage.Render12.Core.Systems.ScreenBuffers");
            return AccessTools.Method(type, "InitializeBuffers");
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Ldc_I4_S &&
                    codes[i].operand is sbyte && (sbyte)codes[i].operand == (sbyte)Plugin.SOURCE_FORMAT)
                {
                    codes[i].operand = (sbyte)Plugin.HDR_FORMAT;
                }
            }

            return codes;
        }
    }

    [HarmonyPatch]
    public static class BufferPlaceholderPatch
    {
        static MethodBase TargetMethod()
        {
            var type = AccessTools.TypeByName("Keen.VRage.Render12.Core.Systems.ScreenBuffers");
            return AccessTools.Method(type, "CreateBackbufferPlaceholder");
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Ldc_I4_S &&
                    codes[i].operand is sbyte && (sbyte)codes[i].operand == (sbyte)Plugin.SOURCE_FORMAT)
                {
                    codes[i].operand = (sbyte)Plugin.HDR_FORMAT;
                }
            }

            return codes;
        }
    }
}
