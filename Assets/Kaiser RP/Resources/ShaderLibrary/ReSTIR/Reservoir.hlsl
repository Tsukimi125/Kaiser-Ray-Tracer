#ifndef KAISER_RAYTRACING_RESERVOIR
#define KAISER_RAYTRACING_RESERVOIR

float unpack_unorm(uint pckd, uint bitCount)
{
    uint maxVal = (1u << bitCount) - 1;
    return float(pckd & maxVal) / maxVal;
}

uint pack_unorm(float val, uint bitCount)
{
    uint maxVal = (1u << bitCount) - 1;
    return uint(clamp(val, 0.0, 1.0) * maxVal);
}

uint pack_11_11_10(float3 color)
{
    color = normalize(color);
    uint pckd = 0;
    pckd += pack_unorm(color.x, 11);
    pckd += pack_unorm(color.y, 11) << 11;
    pckd += pack_unorm(color.z, 10) << 22;
    return pckd;
}

float3 unpack_11_11_10(uint p)
{
    float3 color = float3(
        unpack_unorm(p, 11),
        unpack_unorm(p >> 11, 11),
        unpack_unorm(p >> 22, 10)
    );
    return normalize(color);
}

struct Reservoir
{
    float3 radiance;
    float3 dir;
    float w;
    float wSum;
    int M;

    int4 Pack(int sampleNum = 1024)
    {
        RescaleTo(sampleNum);
        int r = f32tof16(radiance.x) + (f32tof16(radiance.y) << 16);
        int g = f32tof16(radiance.z) + (M << 16);
        int b = asint(wSum);
        int a = asint(w);
        return int4(r, g, b, a);
    }

    void RescaleTo(int sampleNum)
    {
        float scale_M = min(1, float(sampleNum) / max(1, M));
        M = min(M, sampleNum);
        wSum *= scale_M; // 等比缩放

    }

    float TargetPDF(float3 color)
    {
        // radiance to luminance
        return 1e-3 + dot(color, float3(0.299, 0.587, 0.114));
    }

    void Update(float3 newDir, float3 newRadiance,
    float targetWeight, float invSourceWeight,
    float rand)
    {
        float risWeight = invSourceWeight * targetWeight;
        M++;
        wSum += risWeight;
        if (rand < risWeight / max(1e-4, wSum))
        {
            dir = newDir;
            radiance = newRadiance;
            w = risWeight;
        }
    }

    void Update(Reservoir re, float rand)
    {
        if (re.M == 0 || re.w == 0 || re.wSum == 0) return;
        if (M == 0 || w == 0 || wSum == 0)
        {
            dir = re.dir;
            radiance = re.radiance;
            w = re.w;
            wSum = re.wSum;
            M = re.M;
            return;
        }
        wSum += re.wSum;
        if (rand < re.wSum / max(1e-4, wSum))
        {
            dir = re.dir;
            radiance = re.radiance;
            w = re.w;
        }
        M += re.M;
    }
};

Reservoir UnPack(int4 value)
{
    Reservoir re;
    int r = value.r;
    int g = value.g;
    int b = value.b;
    int a = value.a;
    re.radiance = float3(f16tof32(r), f16tof32(r >> 16), f16tof32(g));
    re.M = (g >> 16) & 0xFFFF;
    re.wSum = asfloat(b);
    re.w = asfloat(a);
    // re.dir = unpack_11_11_10(a);
    // // re.sampleIndex = (a >> 16) & 0xFFFF;
    if (isnan(re.wSum) || isnan(re.w))
    {
        re.radiance = 0;
        re.wSum = 0;
        re.w = 0;
        re.M = 0;
    }
    return re;
}


#endif // KAISER_RAYTRACING_RESERVOIR