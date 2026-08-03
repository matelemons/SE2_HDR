namespace SE2HDR.Shaders;

// The HLSL fragments inlined into the shaders we touch.
internal static class HdrHlsl
{
    // Colour space helpers, shared by every patched shader.
    public const string Common = @"// SE2HDR
#ifndef SE2HDR_HDR
#define SE2HDR_HDR

float3 REC709toREC2020(float3 RGB709)
{
    const float3x3 ConvMat = float3x3(
        0.627402, 0.329292, 0.043306,
        0.069095, 0.919544, 0.011360,
        0.016394, 0.088028, 0.895578
    );
    return mul(ConvMat, RGB709);
}

float3 ST2084Curve(float3 L, float maxLuminance)
{
    float m1 = 2610.0 / 4096.0 / 4.0;
    float m2 = 2523.0 / 4096.0 * 128.0;
    float c1 = 3424.0 / 4096.0;
    float c2 = 2413.0 / 4096.0 * 32.0;
    float c3 = 2392.0 / 4096.0 * 32.0;

    // L = FD / 10000, so if FD == 10000, then L = 1.
    float maxLuminanceScale = maxLuminance / 10000.0;
    L *= maxLuminanceScale;

    float3 Lp = pow(L, m1);
    return pow((c1 + c2 * Lp) / (1.0 + c3 * Lp), m2);
}

float4 ToHdr(float4 col, float maxLuminance)
{
    return float4(ST2084Curve(REC709toREC2020(col.rgb), maxLuminance), col.a);
}

#endif";

    // Tone mapping runs in a pass that binds the engine's GlobalSettings buffer, so the peak
    // luminance is written in the unused padding slot at the end of PostProcessSettings.
    public const string PeakNits = @"
float SE2HDR_PeakNits()
{
    return Post_._Padding > 0 ? (float) Post_._Padding : 1000.0;
}";

    // The Slug pipelines bind nothing but their own setup buffer, so we extend it.
    // The declaration below must mirror SlugRenderSetupHdr and the
    // ParamStruct cbuffer the matching vertex shader already declares at b0.
    public const string SlugUiNits = @"
cbuffer SE2HDR_SlugSetup : register(b0)
{
    float4 se2hdrSlugMatrix[4];
    float2 se2hdrSlugViewport;
    float2 se2hdrUi;
};

float SE2HDR_UiNits()
{
    return se2hdrUi.x > 0 ? se2hdrUi.x : 200.0;
}";

    // Sprites get their value from the per-draw pixel constants.
    // PixelConstants is only declared once SpritesShared.hlsli has been
    // included, so this one is written at the use site rather than as a helper.
    public const string SpriteUiNits =
        "(PixelConstants.SE2HDR_UiNits > 0 ? PixelConstants.SE2HDR_UiNits : 200.0)";
}
