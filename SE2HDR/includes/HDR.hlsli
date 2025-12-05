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

float4 ToHdr(float4 col)
{
    float a = col.a;
    float3 color = ST2084Curve(REC709toREC2020(col.rgb), 200);

    return float4(color, a);
}