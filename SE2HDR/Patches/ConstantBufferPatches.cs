using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using HarmonyLib;
using Keen.VRage.Library.Diagnostics;
using Keen.VRage.Library.Mathematics;
using Keen.VRage.Render12.Core.Profiling;
using Keen.VRage.Render12.Core.Systems.CommonResources;
using Keen.VRage.Render12.Primitives.Frame;
using Keen.VRage.Render12.Resources.BindableBuffers;
using Keen.VRage.Render12.UIStage.Sprites;
using Keen.VRage.Render12.UIStage.Vectors;
using SE2HDR.Tools;

namespace SE2HDR.Patches;

// The tone mapping pass binds GlobalSettings, whose PostProcessSettings block ends in an
// unused padding int. Nothing in the engine reads it, so we make use of it to pass the packed
// HDR settings. This runs per frame.
[HarmonyPatch(typeof(SettingsGroup), "CreateFrameSettings")]
public static class FrameSettingsPatch
{
    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original) =>
        ConstantBufferRedirect.Replace(instructions, original,
            "CreateTransientConstantBuffer", typeof(FrameSettings),
            AccessTools.Method(typeof(FrameSettingsPatch), nameof(Create)));

    static TransientConstantBuffer Create(BindableBufferManager buffers, string debugName, in FrameSettings data)
    {
        var patched = data;
        patched.Post._padding = HdrValues.PackedSettings();
        return buffers.CreateTransientConstantBuffer(debugName, in patched);
    }
}

// Writes the paper-white level to Slug pixel shaders.
//
// Those pipelines bind one CBV and two SRVs and nothing else, so the only way in is the
// setup buffer the renderer builds for itself.
[HarmonyPatch(typeof(VectorRenderer), "GetScreenSetup")]
public static class VectorSetupPatch
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct SlugRenderSetupHdr
    {
        public VectorRenderer.SlugRenderSetup Base;
        public float UiNits;
        public float Reserved;
    }

    private static readonly Vector2I NoResolution = new(int.MinValue, int.MinValue);

    static void Prefix(VectorRenderer __instance)
    {
        if (HdrValues.ConsumePaperWhiteNitsChange())
            __instance._screenResolution = NoResolution;
    }

    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original) =>
        ConstantBufferRedirect.Replace(instructions, original,
            "CreatePersistentConstantBuffer", typeof(VectorRenderer.SlugRenderSetup),
            AccessTools.Method(typeof(VectorSetupPatch), nameof(Create)));

    static PersistentConstantBuffer Create(BindableBufferManager buffers, string debugName,
        ref VectorRenderer.SlugRenderSetup data, AllocationGroup allocationGroup)
    {
        var extended = new SlugRenderSetupHdr { Base = data, UiNits = HdrValues.PaperWhiteNits };
        return buffers.CreatePersistentConstantBuffer(debugName, ref extended, allocationGroup);
    }
}

// Carries the paper-white level to the sprite pixel shader, which has no global buffers
// access either. Its per-draw pixel constants are rebuilt for every batch.
[HarmonyPatch(typeof(SpriteRenderer), "Draw")]
public static class SpriteConstantsPatch
{
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    private struct PixelConstantDataHdr
    {
        public SpriteRenderer.PixelConstantData Base;
        public float UiNits;
        public float Reserved;
    }

    static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original) =>
        ConstantBufferRedirect.Replace(instructions, original,
            "CreateTransientConstantBuffer", typeof(SpriteRenderer.PixelConstantData),
            AccessTools.Method(typeof(SpriteConstantsPatch), nameof(Create)));

    static TransientConstantBuffer Create(BindableBufferManager buffers, string debugName,
        in SpriteRenderer.PixelConstantData data)
    {
        var extended = new PixelConstantDataHdr { Base = data, UiNits = HdrValues.PaperWhiteNits };
        return buffers.CreateTransientConstantBuffer(debugName, in extended);
    }
}

// Points a buffer creation call at one of our own, which appends the HDR values and then
// calls through.
internal static class ConstantBufferRedirect
{
    public static IEnumerable<CodeInstruction> Replace(
        IEnumerable<CodeInstruction> instructions,
        MethodBase original,
        string methodName,
        Type dataType,
        MethodInfo replacement)
    {
        var codes = new List<CodeInstruction>(instructions);
        var replaced = 0;

        foreach (var code in codes)
        {
            if (code.operand is not MethodInfo method
                || method.Name != methodName
                || !method.IsGenericMethod
                || method.GetGenericArguments()[0] != dataType)
                continue;

            code.opcode = OpCodes.Call;
            code.operand = replacement;
            replaced++;
        }

        var where = $"{original?.DeclaringType?.Name}.{original?.Name}";
        if (replaced != 1)
            throw new InvalidOperationException(
                $"{where}: expected exactly one {methodName}<{dataType.Name}> call, found {replaced}");

        Log.Default.WriteLine($"[{Plugin.Name}] {where}: constant buffer extended");
        return codes;
    }
}
