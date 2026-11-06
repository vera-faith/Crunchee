Shader "Crunchee/Candy"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _MainTex ("Albedo", 2D) = "white" {}
        _Glossiness ("Gloss", Range(0,1)) = 0.6
        _SpecStrength ("Highlight", Range(0,3)) = 1.2
        _Wrap ("Soft Wrap", Range(0,1)) = 0.45
        _RimColor ("Rim Color", Color) = (1,0.92,0.95,1)
        _RimPower ("Rim Power", Range(0.5,8)) = 3
        _RimStrength ("Rim Strength", Range(0,2)) = 0.35
        [HDR] _EmissionColor ("Emission", Color) = (0,0,0,0)
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200
        Cull [_Cull]
        CGPROGRAM
        #pragma surface CandySurf Candy fullforwardshadows
        #pragma target 3.0
        #include "CandyLighting.cginc"
        ENDCG
    }
    FallBack "Diffuse"
}
