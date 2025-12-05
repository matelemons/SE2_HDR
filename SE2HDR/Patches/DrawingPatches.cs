using HarmonyLib;
using Keen.VRage.Core.Render;
using Keen.VRage.Library.Diagnostics;
using Keen.VRage.Library.Mathematics;
using System.Reflection;
using System.Reflection.Emit;
using Vortice.DXGI;

namespace SE2HDR.Patches
{
    [HarmonyPatch]
    public static class PsoFormatArrayPatch
    {
        static MethodBase TargetMethod()
        {
            // For async methods, we need to patch the compiler-generated state machine's MoveNext
            var spriteRendererType = AccessTools.TypeByName("Keen.VRage.Render12.UIStage.Sprites.SpriteRenderer");

            var stateMachineType = spriteRendererType.GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public)
                .FirstOrDefault(t => t.Name.Contains("InitializeAsync"));

            if (stateMachineType == null)
            {
                return null;
            }


            return AccessTools.Method(stateMachineType, "MoveNext");
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            int patchCount = 0;

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Ldc_I4_S &&
                    codes[i].operand is sbyte && (sbyte)codes[i].operand == (sbyte)Plugin.SOURCE_FORMAT)
                {
                    codes[i].operand = (sbyte)Plugin.HDR_FORMAT;
                    patchCount++;
                }
            }

            Log.Default.WriteLine($"{Plugin.Name} SpriteRenderer async patch: {patchCount} format references patched");
            return codes;
        }
    }

    [HarmonyPatch]
    public static class ScreenshotConstructorPatch
    {
        static MethodBase TargetMethod()
        {
            var type = AccessTools.TypeByName("Keen.VRage.Render12.Core.Systems.ScreenshotsManager");
            return AccessTools.Constructor(type, new Type[] {
                typeof(List<Keen.VRage.Library.Threading.Task>),
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
                    break;
                }
            }

            return codes;
        }
    }

    [HarmonyPatch]
    public static class ScreenshotTakePatch
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            var type = AccessTools.TypeByName("Keen.VRage.Render12.Core.Systems.ScreenshotsManager");
            var method = AccessTools.Method(type, "TakeRequestedScreenshots");

            Log.Default.WriteLine(method.ToString());
            Log.Default.WriteLine(method.ReturnType.ToString());
            Log.Default.WriteLine(method.ContainsGenericParameters.ToString());
            Log.Default.WriteLine(method.GetGenericMethodDefinition().ToString());

            var impl1 = AccessTools.TypeByName("Keen.VRage.Render12.Resources.BindableTextures.RenderTargetTexture");
            var impl2 = AccessTools.TypeByName("Keen.VRage.Render12.Resources.BindableTextures.ResizableRWRenderTargetTexture");

            return [method.MakeGenericMethod(impl1), method.MakeGenericMethod(impl2)];
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);
            int patchCount = 0;

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Ldc_I4_S &&
                    codes[i].operand is sbyte && (sbyte)codes[i].operand == (sbyte)Plugin.SOURCE_FORMAT)
                {
                    codes[i].operand = (sbyte)Plugin.HDR_FORMAT;
                    patchCount++;
                }
            }

            Log.Default.WriteLine($"{Plugin.Name} TakeRequestedScreenshots patch: {patchCount} format references patched");
            return codes;
        }
    }
    
    [HarmonyPatch]
    public static class SceneDrawConstructorPatch
    {
        static MethodBase TargetMethod()
        {
            var type = AccessTools.TypeByName("Keen.VRage.Render12.Core.Systems.SceneDrawSystem");
            return AccessTools.Constructor(type);
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
    public static class ExecutePostPassesPatch
    {
        static MethodBase TargetMethod()
        {
            var type = AccessTools.TypeByName("Keen.VRage.Render12.Core.Systems.SceneDrawSystem");
            return AccessTools.Method(type, "ExecutePostPasses");
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codes = new List<CodeInstruction>(instructions);

            for (int i = 0; i < codes.Count; i++)
            {
                if (codes[i].opcode == OpCodes.Ldc_I4_S &&
                    codes[i].operand is sbyte && ((sbyte)codes[i].operand == (sbyte)Plugin.SOURCE_FORMAT || (sbyte)codes[i].operand == (sbyte)Plugin.SOURCE_FORMAT_UNORM))
                {
                    codes[i].operand = (sbyte)Plugin.HDR_FORMAT;
                }
            }

            return codes;
        }
    }
}
