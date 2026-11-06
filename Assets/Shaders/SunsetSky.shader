Shader "Crunchee/SunsetSky"
{
    Properties
    {
        _TopColor ("Zenith", Color) = (0.42,0.36,0.78,1)
        _MidColor ("Sky", Color) = (0.98,0.55,0.68,1)
        _HorizonColor ("Horizon", Color) = (1,0.72,0.45,1)
        _BottomColor ("Below", Color) = (0.95,0.62,0.7,1)
        _SunColor ("Sun", Color) = (1,0.85,0.6,1)
        _SunDir ("Sun Direction", Vector) = (0.3,0.12,1,0)
        _CloudColor ("Cloud Light", Color) = (1,0.9,0.86,1)
        _CloudShade ("Cloud Shade", Color) = (0.93,0.56,0.66,1)
        _CloudCover ("Cloud Cover", Range(0,1)) = 0.5
        _CloudSpeed ("Cloud Drift", Float) = 0.004
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            half4 _TopColor, _MidColor, _HorizonColor, _BottomColor, _SunColor, _CloudColor, _CloudShade;
            float4 _SunDir; half _CloudCover; float _CloudSpeed;
            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };
            v2f vert(appdata_base v) { v2f o; o.pos = UnityObjectToClipPos(v.vertex); o.dir = v.vertex.xyz; return o; }

            float hash(float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }
            float noise(float2 p)
            {
                float2 i = floor(p), f = frac(p); f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(hash(i), hash(i + float2(1,0)), f.x), lerp(hash(i + float2(0,1)), hash(i + float2(1,1)), f.x), f.y);
            }
            float fbm(float2 p) { float s = 0, a = 0.5; for (int k = 0; k < 4; k++) { s += a * noise(p); p *= 2.03; a *= 0.5; } return s; }

            half4 frag(v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float y = d.y;
                half3 col = lerp(_HorizonColor.rgb, _MidColor.rgb, smoothstep(0.0, 0.28, y));
                col = lerp(col, _TopColor.rgb, smoothstep(0.25, 0.95, y));
                col = lerp(col, _BottomColor.rgb, smoothstep(0.0, -0.25, y));

                float3 sd = normalize(_SunDir.xyz);
                float sun = saturate(dot(d, sd));
                col += _SunColor.rgb * (pow(sun, 8.0) * 0.18 + pow(sun, 64.0) * 0.35 + smoothstep(0.9985, 0.9995, sun) * 0.9);

                // Cloud deck: project onto a dome so puffs flatten toward the horizon.
                float band = smoothstep(-0.05, 0.08, y) * (1.0 - smoothstep(0.55, 0.9, y));
                float2 uv = d.xz / (y + 0.22) * 1.6 + _Time.y * _CloudSpeed * float2(1, 0.3);
                float n = fbm(uv);
                float cover = smoothstep(1.0 - _CloudCover, 1.0 - _CloudCover + 0.25, n) * band;
                float lit = saturate(fbm(uv + sd.xz * 0.15) - n + 0.6);
                half3 cloud = lerp(_CloudShade.rgb, _CloudColor.rgb, lit);
                cloud += _SunColor.rgb * pow(sun, 6.0) * 0.35;
                col = lerp(col, cloud, cover * 0.92);
                return half4(col, 1);
            }
            ENDCG
        }
    }
    FallBack Off
}
