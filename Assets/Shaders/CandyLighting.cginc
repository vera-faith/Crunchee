#ifndef CRUNCHEE_CANDY_LIGHTING
#define CRUNCHEE_CANDY_LIGHTING
#include "UnityPBSLighting.cginc"

sampler2D _MainTex;
fixed4 _Color;
half _Glossiness;
half _Wrap;
fixed4 _RimColor;
half _RimPower;
half _RimStrength;
fixed4 _EmissionColor;
half _SpecStrength;
half _DentAmount;
float4 _CruncheeDent;      // xyz = where she stands (feet), w = dent radius
float _CruncheeDentDepth;  // how deep the squishy is pressed in right now

struct Input
{
    float2 uv_MainTex;
    float3 viewDir;
    float4 color : COLOR;
    float face : VFACE;
};

// Squishy dent: vertices near her feet sink into a soft bowl with a
// small raised lip around it, so the surface visibly gives under her.
// Only materials with _DentAmount > 0 (the squishies) deform.
void CandyVert(inout appdata_full v)
{
    if (_DentAmount <= 0.0 || _CruncheeDentDepth <= 0.0001) return;
    float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
    float r = max(_CruncheeDent.w, 0.01);
    float d = distance(wp.xz, _CruncheeDent.xz);
    float bowl = 1.0 - smoothstep(0.0, r, d);
    float lip = smoothstep(r * 0.7, r * 1.05, d) * (1.0 - smoothstep(r * 1.05, r * 1.8, d));
    float band = saturate((wp.y - (_CruncheeDent.y - 0.9)) / 0.9) * step(wp.y, _CruncheeDent.y + 0.35);
    wp.y += (lip * 0.25 - bowl) * _CruncheeDentDepth * band * _DentAmount;
    v.vertex.xyz = mul(unity_WorldToObject, float4(wp, 1.0)).xyz;
}

// Soft wrapped diffuse + tight glossy highlight + environment sheen:
// reads as candy / jelly / glazed dessert instead of flat plastic.
inline half4 LightingCandy(SurfaceOutput s, half3 viewDir, UnityGI gi)
{
    half3 l = gi.light.dir;
    half nl = dot(s.Normal, l);
    half diff = saturate((nl + _Wrap) / (1.0 + _Wrap));
    half3 h = normalize(l + viewDir);
    half nh = saturate(dot(s.Normal, h));
    half specPow = exp2(s.Gloss * 10.0 + 1.0);
    half spec = pow(nh, specPow) * s.Gloss * _SpecStrength * saturate(nl * 4.0);
    half fres = pow(1.0 - saturate(dot(s.Normal, viewDir)), 4.0);

    half4 c;
    c.rgb = s.Albedo * gi.light.color * diff;
    c.rgb += gi.light.color * spec;
    c.rgb += s.Albedo * gi.indirect.diffuse;
    c.rgb += gi.indirect.specular * s.Gloss * (0.12 + 0.6 * fres);
    c.a = s.Alpha;
    return c;
}

inline void LightingCandy_GI(SurfaceOutput s, UnityGIInput data, inout UnityGI gi)
{
    Unity_GlossyEnvironmentData g = UnityGlossyEnvironmentSetup(s.Gloss, data.worldViewDir, s.Normal, half3(0.04, 0.04, 0.04));
    gi = UnityGlobalIllumination(data, 1.0, s.Normal, g);
}

void CandySurf(Input IN, inout SurfaceOutput o)
{
    fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
    o.Albedo = c.rgb;
    // Tangent-space normal flipped on back faces so two-sided glass/jelly lights correctly.
    o.Normal = float3(0, 0, IN.face >= 0 ? 1 : -1);
    o.Gloss = _Glossiness;
    o.Specular = _Glossiness;
    half rim = pow(1.0 - saturate(dot(normalize(IN.viewDir), o.Normal)), _RimPower);
    o.Emission = _RimColor.rgb * rim * _RimStrength + _EmissionColor.rgb;
    o.Alpha = saturate(c.a + rim * _RimStrength * 0.5 * (1.0 - c.a));
}
#endif
