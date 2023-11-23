#ifndef KAISER_RAYTRACING_RESERVOIR
#define KAISER_RAYTRACING_RESERVOIR


struct MyReservoir
{
    float3 sampleDir; // sample direction
    float W; // unbiased contribution weight of X (sample)
    float weightSum; // sum of resampling weight
    int M; // number of samples
    /*
    * ris weight = misWeight(Xi) * targetWeight(Xi) * W_Xi
    */

    float TargetPDF(float3 color)
    {
        return 1e-2 + dot(color, float3(0.299, 0.587, 0.114));
    }

    float ResampleWeight(float3 targetWeight, float3 sourceWeight)
    {
        return targetWeight / max(1e-3, sourceWeight) / M;
    }

    void Update(float3 newDir, float newWeight, float urand)
    {
        weightSum += newWeight;
        if (urand < (newWeight / max(1e-4, weightSum))) sampleDir = newDir;
    }
};

// struct Reservoir
// {
//     float3 dir;
//     float w;
//     float W_sum;
//     int M;
//     float rand_offset;
//     int sampleIndex;

//     float rand()
//     {
//         float p = frac(sampleIndex++* .1031);
//         p *= p + 33.33;
//         p *= p + p;
//         return frac(p + rand_offset);
//     }

//     int4 Pack(int sampleNum = 1024)
//     {
//         RescaleTo(sampleNum);
//         int r = f32tof16(dir.x) + (f32tof16(dir.y) << 16);
//         int g = f32tof16(dir.z) + (M << 16);
//         int b = asint(W_sum);
//         int a = f32tof16(w) + (sampleIndex << 16);
//         return int4(r, g, b, a);
//     }

//     void RescaleTo(int sampleNum)
//     {
//         float scale_M = min(1, float(sampleNum) / max(1, M));
//         M = min(M, sampleNum);
//         W_sum *= scale_M; // 等比缩放

//     }

//     float TargetPDF(float3 color)
//     {
//         return 1e-2 + dot(color, float3(0.299, 0.587, 0.114)); // radiance to luminance

//     }

//     void Update(float3 newDir, float targetWeight, float invSourceWeight)
//     {
//         float risWeight = invSourceWeight * targetWeight;
//         M++;
//         W_sum += risWeight;
//         if (rand() < risWeight / max(1e-4, W_sum))
//         {
//             dir = newDir;
//             w = targetWeight;
//         }
//     }
//     void Update(Reservoir re)
//     {
//         if (re.M == 0 || re.w == 0 || re.W_sum == 0) return;
//         W_sum += re.W_sum;
//         if (rand() < re.W_sum / max(1e-4, W_sum))
//         {
//             dir = re.dir;
//             w = re.w;
//         }
//         M += re.M;
//     }
// };

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

uint pack_888(float3 color)
{
    color = sqrt(color);
    uint pckd = 0;
    pckd += pack_unorm(color.x, 8);
    pckd += pack_unorm(color.y, 8) << 8;
    pckd += pack_unorm(color.z, 8) << 16;
    return pckd;
}

float3 unpack_888(uint p)
{
    float3 color = float3(
        unpack_unorm(p, 8),
        unpack_unorm(p >> 8, 8),
        unpack_unorm(p >> 16, 8)
    );
    return color * color;
}

struct Reservoir
{
    float3 dir;
    float3 radiance;
    float w;
    float W_sum;
    int M;

    int4 Pack1(int sampleNum = 1024)
    {
        RescaleTo(sampleNum);
        int r = f32tof16(dir.x) + (f32tof16(dir.y) << 16);
        int g = f32tof16(dir.z) + (M << 16);
        int b = f32tof16(W_sum) + (f32tof16(w) << 16);
        int a = pack_888(radiance);
        return int4(r, g, b, a);
    }

    int4 Pack(int sampleNum = 1024)
    {
        RescaleTo(sampleNum);
        int r = pack_888(dir);
        int g = pack_888(radiance);
        int b = asint(W_sum);
        int a = f32tof16(w) + (M << 16);
        return int4(r, g, b, a);
    }

    void RescaleTo(int sampleNum)
    {
        float scale_M = min(1, float(sampleNum) / max(1, M));
        M = min(M, sampleNum);
        W_sum *= scale_M; // 等比缩放

    }

    float TargetPDF(float3 color)
    {
        return 1e-3 + dot(color, float3(0.299, 0.587, 0.114)); // radiance to luminance

    }

    void Update(float3 newDir, float targetWeight, float invSourceWeight, float rand)
    {
        float risWeight = invSourceWeight * targetWeight;
        M++;
        W_sum += risWeight;
        if (rand < risWeight / max(1e-4, W_sum))
        {
            dir = newDir;
            w = risWeight;
        }
    }
    void Update(Reservoir re, float rand)
    {
        if (re.M == 0 || re.w == 0 || re.W_sum == 0) return;
        W_sum += re.W_sum;
        if (rand < re.W_sum / max(1e-4, W_sum))
        {
            dir = re.dir;
            w = re.w;
        }
        M += re.M;
    }
};

Reservoir UnPack1(int4 value)
{
    Reservoir re;
    int r = value.r;
    int g = value.g;
    int b = value.b;
    int a = value.a;
    re.dir = float3(f16tof32(r), f16tof32(r >> 16), f16tof32(g));
    re.M = (g >> 16) & 0xFFFF;
    re.W_sum = f16tof32(b);
    re.w = f16tof32(b >> 16);
    re.radiance = unpack_888(a);
    // re.sampleIndex = (a >> 16) & 0xFFFF;
    if (isnan(re.W_sum) || isnan(re.w))
    {
        re.dir = 0;
        re.W_sum = 0;
        re.w = 0;
        re.M = 0;
    }
    return re;
}

Reservoir UnPack(int4 value)
{
    Reservoir re;
    int r = value.r;
    int g = value.g;
    int b = value.b;
    int a = value.a;
    re.dir = unpack_888(r);
    re.radiance = unpack_888(g);
    re.M = (a >> 16) & 0xFFFF;
    re.W_sum = asfloat(b);
    re.w = f16tof32(a);

    if (isnan(re.W_sum) || isnan(re.w))
    {
        re.dir = 0;
        re.W_sum = 0;
        re.w = 0;
        re.M = 0;
    }

    return re;
}


#endif // KAISER_RAYTRACING_RESERVOIR