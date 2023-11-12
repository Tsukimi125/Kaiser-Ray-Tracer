#ifndef KAISER_RAYTRACING_RESERVOIR
#define KAISER_RAYTRACING_RESERVOIR


struct MyReservoir
{
    float3 sampleDir; // sample direction
    float W; // unbiased contribution weight of X (sample)
    float weightSum; // sum of resampling weight
    /*
    * weight = misWeight(Xi) * phat(Xi) * W_Xi
    */
    int sampleIndex;
    float randOffset;

    float rand()
    {
        float p = frac(sampleIndex++* .1031);
        p *= p + 33.33;
        p *= p + p;
        return frac(p + randOffset);
    }

    void Update(float3 newDir, float newWeight)
    {
        weightSum += newWeight;
        if (rand() < (newWeight / weightSum)) sampleDir = newDir;
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

// re.Update(dir, re.TargetPDF(lerp(Lum, radiance, smooth_metallic)), invPDF);

// struct GIReservoir
// {
//     float3 vPosition;           ///< Visible point's position.
//     float3 vNormal;          ///< Visible point's normal.
//     float3 sPosition;                ///< Hit point's position.
//     float3 sNormal;                  ///< Hit point's normal.
//     float3 radiance;                ///< Chosen sample's radiance.
//     float3 random;
//     int M;                       ///< Input sample count.
//     float avgWeight;
//     uint age;
// }

// struct PackedGIReservoir
// {
//     uint4 vPack;         ///< Visible point's position and normal.
//     uint4 sPack;              ///< Hit point's position and normal.
//     uint4 lightInfo;                ///< Reservoir information.

// };

#endif // KAISER_RAYTRACING_RESERVOIR