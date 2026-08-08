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

    // Tone mapping runs in a pass that binds the engine's GlobalSettings buffer, so our settings
    // are stored in an unused padding slot at the end of PostProcessSettings.
    public const string Tonemap = @"
#define SE2HDR_MODE_LEGACY 0
#define SE2HDR_MODE_AGX 1
#define SE2HDR_MODE_HABLE_EXTENDED 2
#define SE2HDR_MODE_HABLE_HDR 3
#define SE2HDR_MODE_UCHIMURA 4

// The scene-referred curves grade against this reference. BT.2390 fits the result to the panel afterwards. 
#define SE2HDR_REFERENCE_NITS 1000.0

struct SE2HDR_Settings
{
    uint Mode;
    float PeakNits;
    float UiNits;
    float MasterNits;
    float Oversaturate;
    bool Dither;
    float OutputMax;
};

SE2HDR_Settings SE2HDR_GetSettings()
{
    uint packed = asuint(Post_._Padding);

    SE2HDR_Settings s;
    s.PeakNits = (float) (packed & 0xFFF);
    s.UiNits = (float) ((packed >> 12) & 0x1FF);
    s.Oversaturate = (float) ((packed >> 21) & 0x3F) / 63.0;
    s.Dither = ((packed >> 27) & 0x1) != 0;
    s.Mode = (packed >> 28) & 0x7;

    if (s.PeakNits <= 0)
        s.PeakNits = 1000.0;
    if (s.UiNits <= 0)
        s.UiNits = 200.0;

    s.MasterNits = max(SE2HDR_REFERENCE_NITS, s.PeakNits);

    // How far above paper white a curve is allowed to go
    s.OutputMax = max(s.MasterNits / s.UiNits, 1.0);
    return s;
}

float3 SE2HDR_PqFromNits(float3 nits)
{
    const float m1 = 2610.0 / 16384.0;
    const float m2 = 2523.0 / 4096.0 * 128.0;
    const float c1 = 3424.0 / 4096.0;
    const float c2 = 2413.0 / 4096.0 * 32.0;
    const float c3 = 2392.0 / 4096.0 * 32.0;

    float3 Lp = pow(max(nits, 0.0) / 10000.0, m1);
    return pow((c1 + c2 * Lp) / (1.0 + c3 * Lp), m2);
}

float3 SE2HDR_NitsFromPq(float3 pq)
{
    const float m1 = 2610.0 / 16384.0;
    const float m2 = 2523.0 / 4096.0 * 128.0;
    const float c1 = 3424.0 / 4096.0;
    const float c2 = 2413.0 / 4096.0 * 32.0;
    const float c3 = 2392.0 / 4096.0 * 32.0;

    float3 e = pow(max(pq, 0.0), 1.0 / m2);
    return 10000.0 * pow(max(e - c1, 0.0) / max(c2 - c3 * e, 1e-6), 1.0 / m1);
}

// BT.2390 EETF. Takes content mastered at masterNits and fits it to a panel's displayNits. 
// 
// Black lift is ommited.
float3 SE2HDR_Bt2390(float3 nits, float masterNits, float displayNits)
{
    float pqWhite = SE2HDR_PqFromNits(masterNits.xxx).x;
    float maxLum = saturate(SE2HDR_PqFromNits(displayNits.xxx).x / pqWhite);

    // The panel can already show the master, so there is nothing to compress.
    if (maxLum >= 1.0)
        return nits;

    float ks = clamp(1.5 * maxLum - 0.5, 0.0, 0.9999);
    float3 e1 = min(SE2HDR_PqFromNits(nits) / pqWhite, 1.0);

    float3 t = max(e1 - ks, 0.0) / (1.0 - ks);
    float3 t2 = t * t;
    float3 t3 = t2 * t;

    float3 p = (2.0 * t3 - 3.0 * t2 + 1.0) * ks
             + (t3 - 2.0 * t2 + t) * (1.0 - ks)
             + (-2.0 * t3 + 3.0 * t2) * maxLum;

    float3 e2 = lerp(e1, p, (float3) (e1 >= ks));
    return SE2HDR_NitsFromPq(e2 * pqWhite);
}

float3 SE2HDR_DisplayMap(float3 x, SE2HDR_Settings s)
{
    return SE2HDR_Bt2390(x * s.UiNits, s.MasterNits, s.PeakNits) / s.UiNits;
}

float3 SE2HDR_Shoulder(float3 x, float crossover, float value, float slope, float outMax, float highClip)
{
    float shoulderMax = max(outMax - value, 1e-4);
    float span = max(highClip - crossover, 1e-4);
    float w = span * span / shoulderMax * slope;

    float3 d = x - crossover;
    float3 slopeD = slope * d;
    return slopeD * (1.0 + d / w) / (1.0 + slopeD / shoulderMax) + value;
}

// allenwp tone mapping curve, https://allenwp.com/blog/2025/05/29/allenwp-tonemapping-curve/
float3 SE2HDR_AllenWpCurve(float3 x, float outputMax)
{
    const float contrast = 1.25;
    const float crossover = 0.1841865;

    const float crossoverPow = pow(crossover, contrast);
    const float toeA = (1.0 / crossover - 1.0) * crossoverPow;
    const float slopeDenom = crossoverPow + toeA;
    const float slope = contrast * pow(crossover, contrast - 1.0) * toeA / (slopeDenom * slopeDenom);

    float3 s = SE2HDR_Shoulder(x, crossover, crossover, slope, outputMax, max(16.0, outputMax));

    float3 t = pow(x, contrast);
    t = t / (t + toeA);

    return lerp(s, t, (float3) (x < crossover));
}

float3 SE2HDR_TonemapAgx(float3 color, float outputMax)
{
    // Rec.709 to Rec.2020 with the Blender AgX inset matrix, and the inverse outset
    // with Rec.2020 back to Rec.709.
    const float3x3 insetMatrix = float3x3(
        0.544814746488245, 0.373787398372697, 0.0813978551390581,
        0.140416948464053, 0.754137554567394, 0.105445496968552,
        0.0888104196149096, 0.178871756420858, 0.732317823964232
    );

    const float3x3 outsetMatrix = float3x3(
        1.96488741169489, -0.855988495690215, -0.108898916004672,
        -0.299313364904742, 1.32639796461980, -0.0270845997150571,
        -0.164352742528393, -0.238183969428088, 1.40253671195648
    );

    color = mul(insetMatrix, max(color, 0.0));
    color = SE2HDR_AllenWpCurve(color, outputMax);

    color = min(color, outputMax);

    return mul(outsetMatrix, color);
}

#define SE2HDR_HABLE_A 0.15
#define SE2HDR_HABLE_B 0.50
#define SE2HDR_HABLE_C 0.10
#define SE2HDR_HABLE_D 0.20
#define SE2HDR_HABLE_E 0.02
#define SE2HDR_HABLE_F 0.30

float SE2HDR_HableRaw(float x)
{
    return ((x * (SE2HDR_HABLE_A * x + SE2HDR_HABLE_C * SE2HDR_HABLE_B) + SE2HDR_HABLE_D * SE2HDR_HABLE_E)
          / (x * (SE2HDR_HABLE_A * x + SE2HDR_HABLE_B) + SE2HDR_HABLE_D * SE2HDR_HABLE_F))
          - SE2HDR_HABLE_E / SE2HDR_HABLE_F;
}

float3 SE2HDR_HableRaw3(float3 x)
{
    return ((x * (SE2HDR_HABLE_A * x + SE2HDR_HABLE_C * SE2HDR_HABLE_B) + SE2HDR_HABLE_D * SE2HDR_HABLE_E)
          / (x * (SE2HDR_HABLE_A * x + SE2HDR_HABLE_B) + SE2HDR_HABLE_D * SE2HDR_HABLE_F))
          - SE2HDR_HABLE_E / SE2HDR_HABLE_F;
}

float SE2HDR_HableRawSlope(float x)
{
    float n = SE2HDR_HABLE_A * x * x + SE2HDR_HABLE_C * SE2HDR_HABLE_B * x + SE2HDR_HABLE_D * SE2HDR_HABLE_E;
    float d = SE2HDR_HABLE_A * x * x + SE2HDR_HABLE_B * x + SE2HDR_HABLE_D * SE2HDR_HABLE_F;
    float dn = 2.0 * SE2HDR_HABLE_A * x + SE2HDR_HABLE_C * SE2HDR_HABLE_B;
    float dd = 2.0 * SE2HDR_HABLE_A * x + SE2HDR_HABLE_B;
    return (dn * d - n * dd) / (d * d);
}

float SE2HDR_HableInverse(float y, float whiteScale)
{
    float u = y / whiteScale + SE2HDR_HABLE_E / SE2HDR_HABLE_F;
    float qa = SE2HDR_HABLE_A * (1.0 - u);
    float qb = SE2HDR_HABLE_B * (SE2HDR_HABLE_C - u);
    float qc = SE2HDR_HABLE_D * (SE2HDR_HABLE_E - u * SE2HDR_HABLE_F);
    return (-qb + sqrt(max(qb * qb - 4.0 * qa * qc, 0.0))) / (2.0 * max(qa, 1e-5));
}

// Keen's Hable below the knee, an outputMax-aware shoulder above it.
//
// Hable's asymptote is 1 - E/F = 0.9333 before normalisation, so ToneMapFilmic_Hable cannot
// exceed 1.16-1.8x paper white. Splicing at knee keeps everything below 70% of paper white very close to the SDR grade.
// Value and gradient are matched at the crossover to avoid a seam.
float3 SE2HDR_HableHdr(float3 x, float whitePoint, float outputMax)
{
    const float knee = 0.7;

    float whiteScale = 1.0 / SE2HDR_HableRaw(max(whitePoint, 0.5));
    float crossover = SE2HDR_HableInverse(knee, whiteScale);
    float slope = SE2HDR_HableRawSlope(crossover) * whiteScale;

    // Peak is reached at outputMax times the white point
    float highClip = max(whitePoint * outputMax, crossover * 2.0);

    float3 toe = SE2HDR_HableRaw3(x) * whiteScale;
    float3 shoulder = SE2HDR_Shoulder(x, crossover, knee, slope, outputMax, highClip);

    return min(lerp(shoulder, toe, (float3) (x < crossover)), outputMax);
}

// Uchimura's GT curve
float3 SE2HDR_Uchimura(float3 x, float P)
{
    const float a = 1.0;   // midsection gradient
    const float m = 0.10;  // where the linear section starts
    const float l = 0.4;   // linear section length, as a fraction of the range above m
    const float c = 1.20;  // toe curvature
    const float b = 0.0;   // black level

    float l0 = ((P - m) * l) / a;
    float S0 = m + l0;
    float S1 = m + a * l0;
    float C2 = (a * P) / (P - S1);
    float CP = -C2 / P;

    float3 w0 = 1.0 - smoothstep(0.0, m, x);
    float3 w2 = step(S0, x);
    float3 w1 = 1.0 - w0 - w2;

    float3 T = m * pow(max(x, 1e-6) / m, c) + b;
    float3 S = P - (P - S1) * exp(CP * (x - S0));
    float3 L = m + a * (x - m);

    return T * w0 + L * w1 + S * w2;
}

// The GT curve assumes an input scale where diffuse white is around 1.0. Hable normalised
// against WhitePoint ends up with diffuse white around ~0.3. We pre-scale the input to fix that.
//
// This also means our `m` section in SE2HDR_Uchimura is lower than standard to compensate for this.
float3 SE2HDR_UchimuraHdr(float3 x, float whitePoint, float outputMax)
{
    const float anchor = 0.25;

    float whiteScale = 1.0 / SE2HDR_HableRaw(max(whitePoint, 0.5));
    float gain = anchor / max(SE2HDR_HableInverse(anchor, whiteScale), 1e-4);

    return min(SE2HDR_Uchimura(x * gain, outputMax), outputMax);
}

ColorLinear SE2HDR_TonemapScene(ColorLinear color, SE2HDR_Settings s)
{
    if (s.Mode == SE2HDR_MODE_AGX)
    {
        color.Values.rgb = SE2HDR_TonemapAgx(color.Values.rgb, s.OutputMax);
    }
    else if (s.Mode == SE2HDR_MODE_HABLE_HDR)
    {
        color.Values.rgb = SE2HDR_HableHdr(max(color.Values.rgb, 0.0), Post_.WhitePoint, s.OutputMax);
    }
    else if (s.Mode == SE2HDR_MODE_UCHIMURA)
    {
        color.Values.rgb = SE2HDR_UchimuraHdr(max(color.Values.rgb, 0.0), Post_.WhitePoint, s.OutputMax);
    }
    else if (s.Mode == SE2HDR_MODE_HABLE_EXTENDED)
    {
        // EnableSmoothHable is intentionally ignored here. Hable(x) / Hable(x + whitePoint) is
        // bounded below 1.0 for every finite input, because Hable is increasing and
        // both halves converge to the same asymptote. This makes an HDR mod useless.
        color = ToneMapFilmic_Hable(color, Post_.WhitePoint);
    }
    else
    {
        if (Post_.EnableSmoothHable)
            color = ToneMapFilmic_Hable_Smooth(color, Post_.WhitePoint);
        else
            color = ToneMapFilmic_Hable(color, Post_.WhitePoint);

        color = SaturateColor(color);
    }

    return color;
}

float3 SE2HDR_Dither(float3 pq, uint2 texel)
{
    uint h1 = HashMix(texel.x * 73856093u ^ texel.y * 19349663u);
    uint h2 = HashMix(h1 ^ 0x9e3779b9u);

    uint3 a = uint3(h1, h1 >> 10, h1 >> 20) & 0x3FFu;
    uint3 b = uint3(h2, h2 >> 10, h2 >> 20) & 0x3FFu;

    return pq + (float3(a) - float3(b)) / (1023.0 * 1023.0);
}

float4 SE2HDR_Encode(float4 values, SE2HDR_Settings s, uint2 texel)
{
    // Legacy returns a value where 1.0 means display peak, while
    // for scene-referred modes 1.0 means paper white.
    float referenceNits = s.Mode == SE2HDR_MODE_LEGACY ? s.PeakNits : s.UiNits;

    float3 rgb = max(values.rgb, 0.0);

    if (s.Mode != SE2HDR_MODE_LEGACY)
        rgb = SE2HDR_DisplayMap(rgb, s);

    // At 0 the primaries are converted properly. At 1 the Rec.709 values are encoded as Rec.2020,
    // which stretches them across the wider gamut. This is not 'proper', but I left it as an option.
    rgb = lerp(REC709toREC2020(rgb), rgb, s.Oversaturate);

    float3 pq = ST2084Curve(rgb, referenceNits);

    if (s.Dither)
        pq = SE2HDR_Dither(pq, texel);

    return float4(pq, values.a);
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
