Shader "Crunchee/CandyTransparent"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,0.5)
        _MainTex ("Albedo", 2D) = "white" {}
        _Glossiness ("Gloss", Range(0,1)) = 0.85
        _SpecStrength ("Highlight", Range(0,3)) = 1.6
        _Wrap ("Soft Wrap", Range(0,1)) = 0.6
        _RimColor ("Rim Color", Color) = (1,1,1,1)
        _RimPower ("Rim Power", Range(0.5,8)) = 2.5
        _RimStrength ("Rim Strength", Range(0,2)) = 0.5
        [HDR] _EmissionColor ("Emission", Color) = (0,0,0,0)
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }
        LOD 200
        Cull [_Cull]
        ZWrite Off
        CGPROGRAM
        #pragma surface CandySurf Candy alpha:fade
        #pragma target 3.0
        #include "CandyLighting.cginc"
        ENDCG
    }
    FallBack "Transparent/Diffuse"
}
