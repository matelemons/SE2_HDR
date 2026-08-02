using System;
using System.Reflection;
using HarmonyLib;

namespace SE2HDR.Patches;

internal static class PatchTargets
{
    public const string SwapChain = "Keen.VRage.Render12.Core.Device.SwapChain";
    public const string ScreenBuffers = "Keen.VRage.Render12.Core.Systems.ScreenBuffers";
    public const string SceneDrawSystem = "Keen.VRage.Render12.Core.Systems.SceneDrawSystem";
    public const string ScreenshotsManager = "Keen.VRage.Render12.Core.Systems.ScreenshotsManager";
    public const string SpriteRenderer = "Keen.VRage.Render12.UIStage.Sprites.SpriteRenderer";

    public static Type Type(string typeName) =>
        AccessTools.TypeByName(typeName)
        ?? throw new InvalidOperationException($"{typeName} not found");

    // An empty parameter list means "match by name only"
    public static MethodBase Method(string typeName, string name, params Type[] parameters) =>
        AccessTools.Method(Type(typeName), name, parameters.Length == 0 ? null : parameters)
        ?? throw new InvalidOperationException($"{typeName}.{name} not found");

    public static MethodBase Constructor(string typeName, params Type[] parameters) =>
        AccessTools.Constructor(Type(typeName), parameters)
        ?? throw new InvalidOperationException($"{typeName} constructor not found");
}
