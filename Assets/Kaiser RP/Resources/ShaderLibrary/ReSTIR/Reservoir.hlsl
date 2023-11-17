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

struct Reservoir
{
    float3 dir;
    float w;
    float W_sum;
    int M;
    float rand_offset;
    int sampleIndex;
    float rand()
    {
        float p = frac(sampleIndex++* .1031);
        p *= p + 33.33;
        p *= p + p;
        return frac(p + rand_offset);
    }

    float4 Pack(int sampleNum = 1024)
    {
        RescaleTo(sampleNum);
        int r = f32tof16(dir.x) + (f32tof16(dir.y) << 16);
        int g = f32tof16(dir.z) + (M << 16);
        int b = asint(W_sum);
        int a = f32tof16(w) + (sampleIndex << 16);
        return int4(r, g, b, a);
    }

    void RescaleTo(int sampleNum)
    {
        float scale_M = min(1, float(sampleNum) / max(1, M));
        M = min(M, sampleNum);
        W_sum *= scale_M;
    }

    float TargetPDF(float3 color)
    {
        return 1e-2 + dot(color, float3(0.299, 0.587, 0.114));
    }

    void Update(float3 d, float tw, float sw)
    {
        sw *= tw;
        M++;
        W_sum += sw;
        if (rand() < sw / max(1e-4, W_sum))
        {
            dir = d;
            w = tw;
        }
    }
    void Update(Reservoir re)
    {
        if (re.M == 0 || re.w == 0 || re.W_sum == 0) return;
        W_sum += re.W_sum;
        if (rand() < re.W_sum / max(1e-4, W_sum))
        {
            dir = re.dir;
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
    re.dir = float3(f16tof32(r), f16tof32(r >> 16), f16tof32(g));
    re.M = (g >> 16) & 0xFFFF;
    re.W_sum = asfloat(b);
    re.w = f16tof32(a);
    re.sampleIndex = (a >> 16) & 0xFFFF;
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