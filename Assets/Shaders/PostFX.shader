Shader "Hidden/Crunchee/PostFX"
{
    Properties { _MainTex ("", 2D) = "white" {} }
    CGINCLUDE
    #include "UnityCG.cginc"
    sampler2D _MainTex; float4 _MainTex_TexelSize;
    sampler2D _BloomTex;
    half _Threshold, _Knee, _BloomIntensity, _Exposure, _Saturation, _Contrast, _Vignette;
    half4 _BloomTint, _Lift, _Gain;

    half3 Box4(float2 uv, float s)
    {
        float4 o = _MainTex_TexelSize.xyxy * float2(-s, s).xxyy;
        return (tex2D(_MainTex, uv + o.xy).rgb + tex2D(_MainTex, uv + o.zy).rgb +
                tex2D(_MainTex, uv + o.xw).rgb + tex2D(_MainTex, uv + o.zw).rgb) * 0.25;
    }
    half4 FragPrefilter(v2f_img i) : SV_Target
    {
        half3 c = Box4(i.uv, 1);
        half br = max(c.r, max(c.g, c.b));
        half soft = clamp(br - _Threshold + _Knee, 0, 2 * _Knee);
        soft = soft * soft / (4 * _Knee + 1e-4);
        half w = max(soft, br - _Threshold) / max(br, 1e-4);
        return half4(c * w, 1);
    }
    half4 FragDown(v2f_img i) : SV_Target { return half4(Box4(i.uv, 1), 1); }
    half4 FragUp(v2f_img i) : SV_Target { return half4(Box4(i.uv, 0.5), 1); }
    half4 FragComposite(v2f_img i) : SV_Target
    {
        half3 c = tex2D(_MainTex, i.uv).rgb;
        c += tex2D(_BloomTex, i.uv).rgb * _BloomIntensity * _BloomTint.rgb;
        c *= _Exposure;
        c = c / (1 + c * 0.18) * 1.18;                    // gentle highlight roll-off
        c = c * _Gain.rgb + _Lift.rgb * (1 - c);          // warm lift in the shadows
        half l = dot(c, half3(0.299, 0.587, 0.114));
        c = lerp(l.xxx, c, _Saturation);
        c = (c - 0.5) * _Contrast + 0.5;
        float2 v = i.uv - 0.5;
        c *= 1 - dot(v, v) * _Vignette;
        return half4(saturate(c), 1);
    }
    ENDCG
    SubShader
    {
        Cull Off ZWrite Off ZTest Always
        Pass { CGPROGRAM
               #pragma vertex vert_img
               #pragma fragment FragPrefilter
               ENDCG }
        Pass { CGPROGRAM
               #pragma vertex vert_img
               #pragma fragment FragDown
               ENDCG }
        Pass { Blend One One
               CGPROGRAM
               #pragma vertex vert_img
               #pragma fragment FragUp
               ENDCG }
        Pass { CGPROGRAM
               #pragma vertex vert_img
               #pragma fragment FragComposite
               ENDCG }
    }
}
