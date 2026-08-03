using System;
using System.Reflection;
using HarmonyLib;
using Keen.VRage.Core.Plugins;
using Keen.VRage.Library.Diagnostics;
using SE2HDR.Settings;
using SE2HDR.Shaders;
using SE2HDR.Tools;
using Vortice.DXGI;

namespace SE2HDR;

public class Plugin : IPlugin
{
    public const string Name = "HDR10";

    public const Format SOURCE_FORMAT = Format.R8G8B8A8_UNorm_SRgb;
    public const Format SOURCE_FORMAT_UNORM = Format.R8G8B8A8_UNorm;
    public const Format HDR_FORMAT = Format.R10G10B10A2_UNorm;

    public static Plugin Instance;
    internal static ShaderSubstitutions Substitutions { get; private set; }

    // The data directory will be provided by a proper SDK in the future.
    // This static function is currently injected by Pulsar, which will
    // remain compatible, even after the SDK's release.
#pragma warning disable CS0649 // This field is assigned by Pulsar
    private static Func<string, string, string> GetConfigPath;
#pragma warning restore CS0649
    public string DataDir { get; private set; } = GetConfigPath(Name, null);

    public Plugin()
    {
        Instance = this;

        // Force-load Config.Current now that DataDir is available.
        _ = Config.Current;

        Log.Default.WriteLine($"[{Name}] Loaded plugin.");
#if DEBUG
        Harmony.DEBUG = true;
#endif

        if (!Config.Current.Enabled)
        {
            Log.Default.WriteLine($"[{Name}] Disabled in the plugin settings.");
            return;
        }

        // Confirm the shader edits will apply before touching anything.
        var substitutions = new ShaderSubstitutions();
        if (!substitutions.Validate())
        {
            Log.Default.WriteLine(LogSeverity.Error,
                $"[{Name}] Shader validation failed, no render patches applied. HDR has not been enabled.");
            FailureNotice.Queue(
                "Shaders are different than expected. To avoid rendering problems, HDR has not been enabled.\n\n" +
                $"This is likely due to a game update. Please inform the {Name} plugin developer on GitHub.\n\n" +
                "Disable the plugin to stop showing this message.");
            return;
        }

        Substitutions = substitutions;

        var harmony = new Harmony(Name);
        try
        {
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }
        catch (Exception ex)
        {
            // Undo patches if anything fails
            Log.Default.WriteLine(LogSeverity.Error, $"[{Name}] Patching failed, reverting. HDR has not been enabled. {ex}");
            harmony.UnpatchAll(Name);
            Substitutions = null;
            FailureNotice.Queue(
                "Applying the render patches failed, and HDR has not been enabled:\n\n" +
                $"{ex.GetType().Name}: {ex.Message}\n\n" +
                $"This is likely due to a game update. Please inform the {Name} plugin developer on GitHub.\n\n" +
                "Disable the plugin to stop showing this message.");
            return;
        }

        Log.Default.WriteLine($"[{Name}] Applied patches:");
        foreach (var method in harmony.GetPatchedMethods())
            Log.Default.WriteLine($"[{Name}]   - {method.DeclaringType?.Name}.{method.Name}");
    }

    // Invoked by Pulsar via reflection when the user clicks the plugin's config button.
    // ReSharper disable once UnusedMember.Global
    public void OpenConfigDialog()
    {
        try
        {
            var sharedUi = GameAccess.GetSharedUI();
            if (sharedUi == null)
            {
                Log.Default.WriteLine(LogSeverity.Warning, $"[{Name}] SharedUIComponent not available");
                return;
            }

            var generator = new SettingsGenerator();
            var viewModel = new SettingsScreenViewModel(
                generator.Title,
                panel => generator.PopulateContent(panel),
                () => ConfigStorage.Save(Config.Current));

            sharedUi.CreateScreen<SettingsScreen>(viewModel, showCursor: true);
        }
        catch (Exception e)
        {
            Log.Default.WriteLine(LogSeverity.Error, $"[{Name}] OpenConfigDialog failed: {e}");
        }
    }
}
