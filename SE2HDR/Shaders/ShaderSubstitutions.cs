using System;
using System.Collections.Generic;
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
//
// In SDR only the tonemapping pass is touched, other things are not necessary to change.
internal sealed class ShaderSubstitutions
{
    private readonly Dictionary<string, List<Substitution>> byFile;

    public ShaderSubstitutions(bool hdr)
    {
        byFile = BuildSubstitutions(hdr);
    }

    private static Dictionary<string, List<Substitution>> BuildSubstitutions(bool hdr)
    {
        var result = new Dictionary<string, List<Substitution>>(StringComparer.OrdinalIgnoreCase);

        // Tonemapping
        Add(result, "PostProcess/ToneMapping/ToneMapping.hlsl",
            new Substitution
            {
                SearchPattern = "#include <Common/Frame.hlsli>\r\n#include <Common/Random.hlsli>",
                Replacement = "#include <Common/Frame.hlsli>\r\n#include <Common/Random.hlsli>\r\n"
                              + (hdr ? HdrHlsl.Common : "") + HdrHlsl.Tonemap(hdr)
            },
            new Substitution
            {
                SearchPattern = "#ifdef ENABLE_TONE_MAPPING\r\n    color.Values.rgb += GetRelativeLuminance(color).xxx * Post_.BrightDesaturation;\r\n\r\nif (Post_.EnableSmoothHable)\r\n    color = ToneMapFilmic_Hable_Smooth(color, Post_.WhitePoint);\r\nelse\r\n    color = ToneMapFilmic_Hable(color, Post_.WhitePoint);\r\n\r\n#endif\r\n    color = SaturateColor(color);\r\n    ColorSRGB colorSRGB = LinearToSRGB(color);\r\n\r\n#ifdef FILL_ALPHA_LUMINANCE\r\n\tfloat alpha = GetRelativeLuminance((ColorLinear) colorSRGB.Values);\r\n\tDestination[texel] = float4(colorSRGB.Values.rgb, alpha);\r\n#else\r\n\tDestination[texel] = float4(colorSRGB.Values.rgb, 1);\r\n#endif",
                Replacement = "    SE2HDR_Settings se2hdr = SE2HDR_GetSettings();\r\n\r\n#ifdef ENABLE_TONE_MAPPING\r\n    color.Values.rgb += GetRelativeLuminance(color).xxx * Post_.BrightDesaturation;\r\n\r\n    color = SE2HDR_TonemapScene(color, se2hdr);\r\n#else\r\n    if (se2hdr.Mode == SE2HDR_MODE_LEGACY)\r\n        color = SaturateColor(color);\r\n#endif\r\n\r\n    float4 colorSRGB = SE2HDR_Encode(color.Values, se2hdr, texel);\r\n\r\n#ifdef FILL_ALPHA_LUMINANCE\r\n\tfloat alpha = GetRelativeLuminance((ColorLinear) colorSRGB);\r\n\tDestination[texel] = float4(colorSRGB.rgb, alpha);\r\n#else\r\n\tDestination[texel] = float4(colorSRGB.rgb, 1);\r\n#endif"
            });

        if (!hdr)
            return result;

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
                    Replacement = "// @define SHADER_ASSERTS_ENABLED\r\n"
                                  + HdrHlsl.Common + HdrHlsl.SlugUiNits
                },
                new Substitution
                {
                    SearchPattern = "\t#endif\r\n\r\n\t#if defined(SLUG_COVERAGE)",
                    Replacement = "\t#endif\r\n\r\n\tcolor = ToHdr(color, SE2HDR_UiNits());\r\n\r\n\t#if defined(SLUG_COVERAGE)"
                });
        }

        // Sprites
        // The pixel constants are shared with the vertex shader, which never binds them. DXC
        // strips the unused declaration there, so widening the struct only affects the pixel side.
        Add(result, "Primitives/SpritesShared.hlsli",
            new Substitution
            {
                SearchPattern = "struct PixelBufferConstants\r\n{\r\n    uint TextureIndex;\r\n    uint MaskTextureIndex;\r\n};",
                Replacement = "struct PixelBufferConstants\r\n{\r\n    uint TextureIndex;\r\n    uint MaskTextureIndex;\r\n    float SE2HDR_UiNits;\r\n    float SE2HDR_Reserved;\r\n};"
            });

        Add(result, "Primitives/SpritesPixel.hlsl",
            new Substitution
            {
                SearchPattern = "#include <Common/Resources/Managed.hlsli>",
                Replacement = "#include <Common/Resources/Managed.hlsli>\r\n" + HdrHlsl.Common
            },
            new Substitution
            {
                SearchPattern = "    output = (ColorLinearPremultiplied)(sample.Values * input.Color.Values * mask);",
                Replacement = "    output = (ColorLinearPremultiplied)(ToHdr(sample.Values * input.Color.Values * mask, "
                              + HdrHlsl.SpriteUiNits + "));"
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
