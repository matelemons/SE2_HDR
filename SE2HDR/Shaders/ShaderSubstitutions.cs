using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Keen.VRage.Library.Diagnostics;
using SE2HDR.Tools;

namespace SE2HDR.Shaders;

internal sealed class Substitution
{
    public string SearchPattern { get; init; }
    public string Replacement { get; init; }
}

// The set of edits we make to the shader sources, and the logic to apply them.
// Note that all substitutions are applied in-memory when the game loads the shaders.
internal sealed class ShaderSubstitutions
{
    private readonly Dictionary<string, List<Substitution>> byFile;

    public ShaderSubstitutions(int peakNits, int uiNits)
    {
        byFile = BuildSubstitutions(peakNits, uiNits);
    }

    private static Dictionary<string, List<Substitution>> BuildSubstitutions(int peakNits, int uiNits)
    {
        var peak = peakNits.ToString(CultureInfo.InvariantCulture);
        var hdr = HdrHlsl.Build(uiNits);

        var result = new Dictionary<string, List<Substitution>>(StringComparer.OrdinalIgnoreCase);

        // Tonemapping
        Add(result, "PostProcess/ToneMapping/ToneMapping.hlsl",
            new Substitution
            {
                SearchPattern = "#include <Common/Frame.hlsli>\r\n#include <Common/Random.hlsli>",
                Replacement = "#include <Common/Frame.hlsli>\r\n#include <Common/Random.hlsli>\r\n" + hdr
            },
            new Substitution
            {
                SearchPattern = "    color = SaturateColor(color);\r\n    ColorSRGB colorSRGB = LinearToSRGB(color);\r\n\r\n#ifdef FILL_ALPHA_LUMINANCE\r\n\tfloat alpha = GetRelativeLuminance((ColorLinear) colorSRGB.Values);\r\n\tDestination[texel] = float4(colorSRGB.Values.rgb, alpha);\r\n#else\r\n\tDestination[texel] = float4(colorSRGB.Values.rgb, 1);\r\n#endif",
                Replacement = "    color = SaturateColor(color);\r\n\r\n    // Color sRGB? that's a lie\r\n    // OVERSATURATED\r\n    // (you can enable this if you want very vivid colors, i suppose)\r\n    //float4 colorSRGB = float4(color.Values.rgb, color.Values.a);\r\n\r\n    // CORRECT\r\n    float4 colorSRGB = float4(REC709toREC2020(color.Values.rgb), color.Values.a);\r\n    \r\n    colorSRGB = float4(ST2084Curve(colorSRGB.rgb, " + peak + "), colorSRGB.a);\r\n\r\n#ifdef FILL_ALPHA_LUMINANCE\r\n\tfloat alpha = GetRelativeLuminance((ColorLinear) colorSRGB);\r\n\tDestination[texel] = float4(colorSRGB.rgb, alpha);\r\n#else\r\n\tDestination[texel] = float4(colorSRGB.rgb, 1);\r\n#endif"
            });

        // UI
        foreach (var path in new[]
                 {
                     "Primitives/VectorFontPixel.hlsl",
                     "Primitives/VectorGeneralPixel.hlsl",
                     "Primitives/VectorMultiColorPixel.hlsl"
                 })
        {
            Add(result, path,
                new Substitution
                {
                    SearchPattern = "// @define SHADER_ASSERTS_ENABLED",
                    Replacement = "// @define SHADER_ASSERTS_ENABLED\r\n" + hdr
                },
                new Substitution
                {
                    SearchPattern = "\t#endif\r\n\r\n\t#if defined(SLUG_COVERAGE)",
                    Replacement = "\t#endif\r\n\r\n\tcolor = ToHdr(color);\r\n\r\n\t#if defined(SLUG_COVERAGE)"
                });
        }

        // Sprites
        Add(result, "Primitives/SpritesPixel.hlsl",
            new Substitution
            {
                SearchPattern = "#include <Common/Resources/Managed.hlsli>",
                Replacement = "#include <Common/Resources/Managed.hlsli>\r\n" + hdr
            },
            new Substitution
            {
                SearchPattern = "    output = (ColorLinearPremultiplied)(sample.Values * input.Color.Values * mask);",
                Replacement = "    output = (ColorLinearPremultiplied)(ToHdr(sample.Values * input.Color.Values * mask));"
            });

        // Bilinear upscaling
        // (this is necessary because it seems like the game does the sRGB conversion in this stage. FSR seems to handle it,
        // but FXAA or no AA need this extra patch)
        Add(result, "PostProcess/Upsampling/BilinearUpsampling.hlsl",
            new Substitution
            {
                SearchPattern = "    OutputTexture[gxy] = AMD_FSR_TO_SRGB(InputTexture.SampleLevel(LinearSampler, pp, 0.0));",
                Replacement = "    OutputTexture[gxy] = pow(AMD_FSR_TO_SRGB(InputTexture.SampleLevel(LinearSampler, pp, 0.0)), 2.2);"
            });

        return result;
    }

    private static void Add(Dictionary<string, List<Substitution>> target, string relativePath,
        params Substitution[] substitutions)
    {
        target[Normalize(relativePath)] = new List<Substitution>(substitutions);
    }
    
    public string Apply(string relativePath, string content)
    {
        // Unaffected by HDR patch
        if (content == null || !byFile.TryGetValue(Normalize(relativePath), out var substitutions))
            return content;

        foreach (var substitution in substitutions)
        {
            if (!content.Contains(substitution.SearchPattern))
            {
                // Validate() checked the same files at startup, so reaching here means the engine
                // read something other than what we checked.
                Log.Default.WriteLine(LogSeverity.Error,
                    $"[{Plugin.Name}] Pattern not found in {relativePath} at compile time. That shader is left unpatched.");
                continue;
            }

            content = content.Replace(substitution.SearchPattern, substitution.Replacement);
        }

        return content;
    }

    // Reads the shader sources off disk without modifying them to confirm every pattern
    // still matches. This is done to prevent applying edits if the original sources were considerably modified.
    public bool Validate()
    {
        var gameRoot = GamePaths.FindGameRoot();
        if (gameRoot == null)
            return false;

        Log.Default.WriteLine($"[{Plugin.Name}] Game installation: {gameRoot}");

        var shaderDir = Path.Combine(gameRoot, GamePaths.ShaderSubDirectory);
        var failures = new StringBuilder();
        var checkedCount = 0;

        foreach (var (relativePath, substitutions) in byFile)
        {
            var filePath = Path.Combine(shaderDir, relativePath.Replace('/', Path.DirectorySeparatorChar));

            string content;
            try
            {
                content = File.ReadAllText(filePath);
            }
            catch (Exception ex)
            {
                failures.Append($"\r\n  {relativePath}: {ex.Message}");
                continue;
            }

            for (var i = 0; i < substitutions.Count; i++)
            {
                if (content.Contains(substitutions[i].SearchPattern))
                    checkedCount++;
                else
                    failures.Append($"\r\n  {relativePath}: edit {i + 1} does not match");
            }
        }

        if (failures.Length > 0)
        {
            Log.Default.WriteLine(LogSeverity.Error,
                $"[{Plugin.Name}] The game's shaders do not match :{failures}");
            return false;
        }

        Log.Default.WriteLine($"[{Plugin.Name}] Shader sources validated ({checkedCount} edits across {byFile.Count} files).");
        return true;
    }
    
    private static string Normalize(string path)
    {
        var segments = new List<string>();

        foreach (var segment in path.Replace('\\', '/').Split('/'))
        {
            if (segment.Length == 0 || segment == ".")
                continue;

            if (segment == "..")
            {
                if (segments.Count > 0)
                    segments.RemoveAt(segments.Count - 1);
                continue;
            }

            segments.Add(segment);
        }

        return string.Join("/", segments);
    }
}
