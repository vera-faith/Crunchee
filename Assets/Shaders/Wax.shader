Shader "Crunchee/Wax"
{
    Properties
    {
        _Color ("Milky Tint", Color) = (1,0.99,0.97,1)
        _MainTex ("Albedo", 2D) = "white" {}
        _Thickness ("Wax Build", Range(0,1)) = 0
        _Frost ("Stress Frost", Range(0,1)) = 0
        _Glossiness ("Gloss", Range(0,1)) = 0.92
        _SpecStrength ("Sun Specular", Range(0,4)) = 2.2
        _EnvStrength ("Sky Reflection", Range(0,1)) = 0.45
        _Wrap ("Soft Wrap", Range(0,1)) = 0.5
        _DentAmount ("Squish Dent", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "IgnoreProjector"="True" }
        LOD 200
        Cull [_Cull]
        ZWrite Off
        CGPROGRAM
        // CandyVert (in the shared cginc) is required: it is what lets the
        // coat bend into the squish dent under her feet.
        #pragma surface WaxSurf Wax alpha:fade vertex:CandyVert
        #pragma target 3.0
        #include "CandyLighting.cginc"

        half _Thickness;
        half _Frost;

        // Soft wrapped diffuse for the milky veil, a STRONG tight sun
        // specular, and a fresnel-weighted sky reflection - a clear glossy
        // coat, not a glowing rim.
        inline half4 LightingWax(SurfaceOutput s, half3 viewDir, UnityGI gi)
        {
            half3 l = gi.light.dir;
            half nl = dot(s.Normal, l);
            half diff = saturate((nl + _Wrap) / (1.0 + _Wrap));
            half3 h = normalize(l + viewDir);
            half nh = saturate(dot(s.Normal, h));
            half specPow = exp2(s.Gloss * 12.0 + 2.0);
            half spec = pow(nh, specPow) * s.Gloss * _SpecStrength * saturate(nl * 4.0);
            half fres = pow(1.0 - saturate(dot(s.Normal, viewDir)), 4.0);

            half4 c;
            c.rgb = s.Albedo * gi.light.color * diff;
            c.rgb += gi.light.color * spec;
            c.rgb += s.Albedo * gi.indirect.diffuse;
            c.rgb += gi.indirect.specular * s.Gloss * (0.15 + 0.85 * fres) * _EnvStrength;
            c.a = s.Alpha;
            return c;
        }

        inline void LightingWax_GI(SurfaceOutput s, UnityGIInput data, inout UnityGI gi)
        {
            Unity_GlossyEnvironmentData g = UnityGlossyEnvironmentSetup(s.Gloss, data.worldViewDir, s.Normal, half3(0.04, 0.04, 0.04));
            gi = UnityGlobalIllumination(data, 1.0, s.Normal, g);
        }

        void WaxSurf(Input IN, inout SurfaceOutput o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Normal = float3(0, 0, IN.face >= 0 ? 1 : -1);
            o.Gloss = _Glossiness;
            o.Specular = _Glossiness;
            // The milky veil grows with the coat build; the butter always
            // reads through it. _Frost stress-whitens cracked plates so they
            // contrast with the clean yellow butter showing in the gaps.
            o.Albedo = lerp(c.rgb * lerp(0.55f, 1.0f, _Thickness), half3(1, 1, 1), _Frost * 0.35f);
            // Base alpha from the coat build (0.05 -> 0.38) + _Frost; the
            // fresnel edge boost is CAPPED so grazing angles turn glassy,
            // never opaque white, and there is NO rim emission (the haze).
            half rim = pow(1.0 - saturate(dot(normalize(IN.viewDir), o.Normal)), 3.0);
            half base = lerp(0.05f, 0.38f, _Thickness) + _Frost * 0.3f;
            o.Alpha = saturate((base + min(rim, 0.25f)) * c.a);
        }
        ENDCG
    }
    FallBack "Transparent/Diffuse"
}
