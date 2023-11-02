#ifndef KAISER_RAYTRACING_RESERVOIR
#define KAISER_RAYTRACING_RESERVOIR


struct Reservoir
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


// struct Reservoir
// {
//     float3 dir;
//     float w;
//     float wSum;
//     int M;
//     float randOffset;
//     int sampleIndex;

//     float rand()
//     {
//         float p = frac(sampleIndex++* .1031);
//         p *= p + 33.33;
//         p *= p + p;
//         return frac(p + randOffset);
//     }

//     void Update(float3 inputDir, float targetWeight, float sourceWeight) {
//         sourceWeight *= targetWeight;
//         M++;
//         wSum += sourceWeight;
//         if (rand() < sourceWeight / max(1e-4, wSum))
//         {
//             dir = inputDir;
//             w = targetWeight;
//         }
//     }

//     void Merge(Reservoir r)
//     {
//         if (r.M == 0 || r.w == 0 || r.W_sum == 0) return;
//         wSum += r.wSum;
//         if (rand() < re.wSum / max(1e-4, wSum))
//         {
//             dir = r.dir;
//             w = r.w;
//         }

//         M += other.M;
//     }
// }

#endif // KAISER_RAYTRACING_RESERVOIR