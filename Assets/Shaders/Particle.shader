Shader "Crunchee/Particle"
{
    Properties
    {
        _MainTex ("Sprite", 2D) = "white" {}
        [HDR] _TintColor ("Tint", Color) = (1,1,1,1)
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 10
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" }
        Blend SrcAlpha [_DstBlend]
        ZWrite Off Cull Off Lighting Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            sampler2D _MainTex; float4 _MainTex_ST; half4 _TintColor;
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; half4 col : COLOR; UNITY_FOG_COORDS(1) };
            v2f vert(appdata_full v)
            {
                v2f o; o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.texcoord, _MainTex); o.col = v.color * _TintColor;
                UNITY_TRANSFER_FOG(o, o.pos); return o;
            }
            half4 frag(v2f i) : SV_Target
            {
                half4 c = tex2D(_MainTex, i.uv) * i.col;
                UNITY_APPLY_FOG_COLOR(i.fogCoord, c, half4(0,0,0,0));
                return c;
            }
            ENDCG
        }
    }
}
