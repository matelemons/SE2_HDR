using HarmonyLib;
using Keen.Game2.Game.Plugins;
using Keen.VRage.Library.Diagnostics;
using System.Reflection;
using Vortice.DXGI;

namespace SE2HDR
{
    public class Plugin : IPlugin
    {
        public const string Name = "SE2HDR";
        public const Format SOURCE_FORMAT = Format.R8G8B8A8_UNorm_SRgb;
        public const Format SOURCE_FORMAT_UNORM = Format.R8G8B8A8_UNorm;
        public const Format HDR_FORMAT = Format.R10G10B10A2_UNorm;


        public Plugin()
        {
            Log.Default.WriteLine($"[{Name}] Loaded plugin.");
#if DEBUG
            Harmony.DEBUG = true;
#endif
            Harmony harmony = new Harmony(Name);
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            string gameRootPath = Directory.GetCurrentDirectory();

            Log.Default.WriteLine($"[{Name}] Applied patches:");
            var patches = harmony.GetPatchedMethods();
            foreach (var method in patches)
            {
                Log.Default.WriteLine($"[{Name}]   - {method.DeclaringType?.Name}.{method.Name}");
            }

            // Replace shaders
            try
            {
                ShaderPatcher patcher = new(gameRootPath);
                patcher.TryPatchShaders();
            }
            catch (Exception ex)
            {
                Log.Default.WriteLine($"[{Name}] Error during shader patching: {ex.Message}");
            }
        }
    }
}
