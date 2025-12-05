using HarmonyLib;
using Keen.VRage.Core.Render;
using Keen.VRage.Library.Diagnostics;
using Keen.VRage.Library.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using Vortice.DXGI;

namespace SE2HDR.Patches
{
    [HarmonyPatch]
    public static class SwapchainPatch
    {
        static MethodBase TargetMethod()
        {
            var type = AccessTools.TypeByName("Keen.VRage.Render12.Core.Device.SwapChain");
            return AccessTools.Method(type, "CreateD3DSwapChain");
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
                    break;
                }
            }

            return codes;
        }

        static void Postfix(ref IDXGISwapChain3 __result)
        {
            if (__result == null)
            {
                Log.Default.WriteLine($"{Plugin.Name} ERROR: Swapchain creation returned null!");
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
                    Log.Default.WriteLine($"{Plugin.Name} WARNING: SetColorSpace1 failed: " + e);
                    Log.Default.WriteLine($"{Plugin.Name} This might mean your display doesn't support HDR, or HDR is not enabled in Windows settings");
                }
            }
            catch (Exception ex)
            {
                Log.Default.WriteLine($"{Plugin.Name} ERROR setting color space: {ex.Message}");
            }
        }
    }

    [HarmonyPatch]
    public static class SwapchainResizePatch
    {
        static MethodBase TargetMethod()
        {
            var type = AccessTools.TypeByName("Keen.VRage.Render12.Core.Device.SwapChain");
            return AccessTools.Method(type, "Resize", new Type[] { typeof(Vector2I) });
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
    public static class SwapchainInitializeBackBufferPatch
    {
        static MethodBase TargetMethod()
        {
            var type = AccessTools.TypeByName("Keen.VRage.Render12.Core.Device.SwapChain");
            return AccessTools.Method(type, "InitializeBackBufferWrappers");
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
    public static class SwapchainConstructorPatch
    {
        static MethodBase TargetMethod()
        {
            var type = AccessTools.TypeByName("Keen.VRage.Render12.Core.Device.SwapChain");
            return AccessTools.Constructor(type, new Type[] {
                typeof(RenderDisplaySettings).MakeByRefType(),
                AccessTools.TypeByName("Keen.VRage.Core.Platform.IPlatformWindows")
            });
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
    public static class SwapchainUpdatePatch
    {
        static MethodBase TargetMethod()
        {
            var type = AccessTools.TypeByName("Keen.VRage.Render12.Core.Device.SwapChain");
            return AccessTools.Method(type, "Update");
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
