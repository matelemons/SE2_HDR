using System;
using System.Reflection;
using HarmonyLib;
using Keen.VRage.Core.Render;
using Keen.VRage.Library.Diagnostics;

namespace SE2HDR.Patches;

// Rewrites shader sources as the engine reads them.
// Caching/hashing is handled automatically by the engine and is unaffected by this patch.
[HarmonyPatch]
internal static class ShaderSourcePatch
{
    static MethodBase TargetMethod()
    {
        var type = AccessTools.TypeByName("Keen.VRage.Render12.Resources.Shaders.ShaderFileCache")
                   ?? throw new InvalidOperationException("ShaderFileCache not found");

        return AccessTools.Method(type, "Create")
               ?? throw new InvalidOperationException("ShaderFileCache.Create not found");
    }

    static void Prefix(ref string content, ShaderFileHandle shaderHandle)
    {
        var substitutions = Plugin.Substitutions;
        if (substitutions == null)
            return;

        try
        {
            content = substitutions.Apply(shaderHandle.RelativePath, content);
        }
        catch (Exception ex)
        {
            Log.Default.WriteLine(LogSeverity.Error,
                $"[{Plugin.Name}] Failed to rewrite {shaderHandle.RelativePath}: {ex}");
        }
    }
}
