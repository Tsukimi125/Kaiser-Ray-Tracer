Shader "KaiserRenderPipeline/Lit"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _MainTex ("Albedo", 2D) = "white" { }
        _Glossiness ("Smoothness", Range(0, 1)) = 1.0
        _Metallic ("Metallic", Range(0, 1)) = 0.0
        _SpecularColor ("SpecularColor", Color) = (1, 1, 1, 1)
        [NoScaleOffset] _BumpMap ("Normal", 2D) = "bump" { }
        [NoScaleOffset] _MetallicGlossMap ("Metallic", 2D) = "white" { }
        _IOR ("Index of Refraction", Range(1.0, 2.8)) = 1.5
        
        [Toggle(_EMISSION)]_Emission ("Emission", float) = 0
        [HDR]_EmissionColor ("EmissionColor", Color) = (0, 0, 0)
        _EmissionTex ("Emission", 2D) = "white" { }

        [Toggle(_TRANSPARENT)]_Transparent ("Transparent", float) = 0
        _ExtinctionCoefficient ("Extinction Coefficient", Range(0.0, 20.0)) = 1.0
    }

    SubShader
    {
        Pass
        {
            Name "RayTracing"
            Tags { "LightMode" = "RayTracing" }

            HLSLPROGRAM

            #include "../ShaderLibrary/RayTracingHit.hlsl"

            ENDHLSL
        }
    }

    FallBack "Diffuse"

    CustomEditor "KaiserLitShaderGUI"
}
