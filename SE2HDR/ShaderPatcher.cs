using Keen.VRage.Library.Diagnostics;
using System.Reflection;

namespace SE2HDR
{
    public class ShaderPatch
    {
        public string RelativePath { get; set; }
        public string SearchPattern { get; set; }
        public string Replacement { get; set; }
    }

    public class ShaderPatcher
    {
        private const string ShaderSubDirectory = "VRage/GameData/Engine/Shaders";
        private const string BackupSuffix = ".backup";
        private const string HdrIncludeFile = "Common/HDR.hlsli";

        private readonly string gamePath;
        private readonly List<ShaderPatch> patches = new List<ShaderPatch>();

        public ShaderPatcher(string gamePath)
        {
            this.gamePath = gamePath;
            InitializePatches();
        }

        private void InitializePatches()
        {
            // Tonemapping
            patches.Add(new ShaderPatch
            {
                RelativePath = "PostProcess/ToneMapping/ToneMapping.hlsl",
                SearchPattern = "#include <Common/Frame.hlsli>\r\n#include <Common/Random.hlsli>",
                Replacement = "#include <Common/Frame.hlsli>\r\n#include <Common/Random.hlsli>\r\n#include <Common/HDR.hlsli>"
            });

            patches.Add(new ShaderPatch
            {
                RelativePath = "PostProcess/ToneMapping/ToneMapping.hlsl",
                SearchPattern = "    color = SaturateColor(color);\r\n    ColorSRGB colorSRGB = LinearToSRGB(color);\r\n\r\n#ifdef FILL_ALPHA_LUMINANCE\r\n\tfloat alpha = GetRelativeLuminance((ColorLinear) colorSRGB.Values);\r\n\tDestination[texel] = float4(colorSRGB.Values.rgb, alpha);\r\n#else\r\n\tDestination[texel] = float4(colorSRGB.Values.rgb, 1);\r\n#endif",
                Replacement = "    color = SaturateColor(color);\r\n\r\n    // Color sRGB? that's a lie\r\n    // OVERSATURATED\r\n    // (you can enable this if you want very vivid colors, i suppose)\r\n    //float4 colorSRGB = float4(color.Values.rgb, color.Values.a);\r\n\r\n    // CORRECT\r\n    float4 colorSRGB = float4(REC709toREC2020(color.Values.rgb), color.Values.a);\r\n    \r\n    colorSRGB = float4(ST2084Curve(colorSRGB.rgb, 1000), colorSRGB.a);\r\n\r\n#ifdef FILL_ALPHA_LUMINANCE\r\n\tfloat alpha = GetRelativeLuminance((ColorLinear) colorSRGB);\r\n\tDestination[texel] = float4(colorSRGB.rgb, alpha);\r\n#else\r\n\tDestination[texel] = float4(colorSRGB.rgb, 1);\r\n#endif"
            });

            // Fonts
            patches.Add(new ShaderPatch
            {
                RelativePath = "Primitives/VectorFontPixel.hlsl",
                SearchPattern = "// @define SHADER_ASSERTS_ENABLED",
                Replacement = "// @define SHADER_ASSERTS_ENABLED\r\n#include <Common/HDR.hlsli>"
            });

            patches.Add(new ShaderPatch
            {
                RelativePath = "Primitives/VectorFontPixel.hlsl",
                SearchPattern = "\t#endif\r\n\r\n\t#if defined(SLUG_COVERAGE)",
                Replacement = "\t#endif\r\n\r\n\tcolor = ToHdr(color);\r\n\r\n\t#if defined(SLUG_COVERAGE)"
            });

            // Vectors general
            patches.Add(new ShaderPatch
            {
                RelativePath = "Primitives/VectorGeneralPixel.hlsl",
                SearchPattern = "// @define SHADER_ASSERTS_ENABLED",
                Replacement = "// @define SHADER_ASSERTS_ENABLED\r\n#include <Common/HDR.hlsli>"
            });

            patches.Add(new ShaderPatch
            {
                RelativePath = "Primitives/VectorGeneralPixel.hlsl",
                SearchPattern = "\t#endif\r\n\r\n\t#if defined(SLUG_COVERAGE)",
                Replacement = "\t#endif\r\n\r\n\tcolor = ToHdr(color);\r\n\r\n\t#if defined(SLUG_COVERAGE)"
            });

            // Vectors multicolor
            patches.Add(new ShaderPatch
            {
                RelativePath = "Primitives/VectorMultiColorPixel.hlsl",
                SearchPattern = "// @define SHADER_ASSERTS_ENABLED",
                Replacement = "// @define SHADER_ASSERTS_ENABLED\r\n#include <Common/HDR.hlsli>"
            });

            patches.Add(new ShaderPatch
            {
                RelativePath = "Primitives/VectorMultiColorPixel.hlsl",
                SearchPattern = "\t#endif\r\n\r\n\t#if defined(SLUG_COVERAGE)",
                Replacement = "\t#endif\r\n\r\n\tcolor = ToHdr(color);\r\n\r\n\t#if defined(SLUG_COVERAGE)"
            });


            // Sprites
            patches.Add(new ShaderPatch
            {
                RelativePath = "Primitives/SpritesPixel.hlsl",
                SearchPattern = "#include <Common/Resources/Managed.hlsli>",
                Replacement = "#include <Common/Resources/Managed.hlsli>\r\n#include <Common/HDR.hlsli>"
            });

            patches.Add(new ShaderPatch
            {
                RelativePath = "Primitives/SpritesPixel.hlsl",
                SearchPattern = "    output = (ColorLinearPremultiplied)(sample.Values * input.Color.Values * mask);",
                Replacement = "    output = (ColorLinearPremultiplied)(ToHdr(sample.Values * input.Color.Values * mask));"
            });


            // Bilinear upscaling
            // (this is necessary because it seems like the game does the sRGB conversion in this stage. FSR seems to handle it,
            // but FXAA or no AA need this extra patch)
            patches.Add(new ShaderPatch
            {
                RelativePath = "PostProcess/Upsampling/BilinearUpsampling.hlsl",
                SearchPattern = "    OutputTexture[gxy] = AMD_FSR_TO_SRGB(InputTexture.SampleLevel(LinearSampler, pp, 0.0));",
                Replacement = "    OutputTexture[gxy] = pow(AMD_FSR_TO_SRGB(InputTexture.SampleLevel(LinearSampler, pp, 0.0)), 2.2);"
            });
        }

        public bool TryPatchShaders()
        {
            try
            {
                if (string.IsNullOrEmpty(gamePath))
                {
                    Log.Default.WriteLine($"{Plugin.Name} Game path not set in preferences. Skipping shader patching.");
                    return false;
                }

                if (!Directory.Exists(gamePath))
                {
                    Log.Default.WriteLine($"{Plugin.Name} Game path does not exist: {gamePath}");
                    return false;
                }

                string shaderDir = Path.Combine(gamePath, ShaderSubDirectory);
                if (!Directory.Exists(shaderDir))
                {
                    Log.Default.WriteLine($"{Plugin.Name} Shader directory does not exist: {shaderDir}");
                    return false;
                }

                // Check if already patched
                string hdrMarkerPath = Path.Combine(shaderDir, HdrIncludeFile);
                if (File.Exists(hdrMarkerPath))
                {
                    Log.Default.WriteLine($"{Plugin.Name} Shaders already patched (HDR.hlsli exists). Skipping.");
                    return true;
                }

                Log.Default.WriteLine($"{Plugin.Name} Starting shader patching process...");

                BackupShaders(shaderDir);
                CopyHdrIncludeFile(shaderDir);

                ApplyShaderPatches(shaderDir);

                Log.Default.WriteLine($"{Plugin.Name} Shader patching completed successfully.");
                return true;
            }
            catch (Exception ex)
            {
                Log.Default.WriteLine($"{Plugin.Name} ERROR during shader patching: {ex.Message}");
                Log.Default.WriteLine($"{Plugin.Name} Stack trace: {ex.StackTrace}");
                return false;
            }
        }

        private void BackupShaders(string shaderDir)
        {
            string backupDir = shaderDir + BackupSuffix;

            if (Directory.Exists(backupDir))
            {
                Log.Default.WriteLine($"{Plugin.Name} Backup already exists. Skipping backup.");
                return;
            }

            Log.Default.WriteLine($"{Plugin.Name} Creating backup: {backupDir}");
            CopyDirectory(shaderDir, backupDir);
            Log.Default.WriteLine($"{Plugin.Name} Backup created successfully.");
        }

        private void CopyHdrIncludeFile(string shaderDir)
        {
            string commonDir = Path.Combine(shaderDir, "Common");
            string targetPath = Path.Combine(commonDir, "HDR.hlsli");

            Log.Default.WriteLine($"{Plugin.Name} Copying HDR.hlsli to: {targetPath}");

            var assembly = Assembly.GetExecutingAssembly();
            string resourceName = "SE2HDR.includes.HDR.hlsli";

            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    throw new FileNotFoundException($"Embedded resource '{resourceName}' not found in assembly");
                }

                using (FileStream fileStream = File.Create(targetPath))
                {
                    stream.CopyTo(fileStream);
                }
            }

            Log.Default.WriteLine($"{Plugin.Name} HDR.hlsli copied successfully.");
        }

        private void ApplyShaderPatches(string shaderDir)
        {
            Log.Default.WriteLine($"{Plugin.Name} Applying shader patches...");

            int successCount = 0;
            int failCount = 0;

            foreach (var patch in patches)
            {
                string filePath = Path.Combine(shaderDir, patch.RelativePath);

                try
                {
                    PatchFile(filePath, patch.SearchPattern, patch.Replacement);
                    successCount++;
                }
                catch (Exception ex)
                {
                    Log.Default.WriteLine($"{Plugin.Name} Failed to patch {patch.RelativePath}: {ex.Message}");
                    failCount++;
                }
            }

            Log.Default.WriteLine($"{Plugin.Name} Shader patching complete: {successCount} successful, {failCount} failed");
        }

        private void CopyDirectory(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);

            foreach (string file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
            {
                string relativePath = file.Substring(sourceDir.Length + 1);
                string destFile = Path.Combine(destDir, relativePath);

                Directory.CreateDirectory(Path.GetDirectoryName(destFile));
                File.Copy(file, destFile, false);
            }
        }

        private void PatchFile(string filePath, string searchPattern, string replacement)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Shader file not found: {filePath}");
            }

            string content = File.ReadAllText(filePath);

            if (!content.Contains(searchPattern))
            {
                throw new InvalidOperationException($"Pattern not found in {Path.GetFileName(filePath)}");
            }

            string patchedContent = content.Replace(searchPattern, replacement);
            File.WriteAllText(filePath, patchedContent);

            Log.Default.WriteLine($"{Plugin.Name} Successfully patched {Path.GetFileName(filePath)}");
        }
    }
}
