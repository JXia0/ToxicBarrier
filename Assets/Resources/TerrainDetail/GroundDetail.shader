Shader "ToxicBarrier/GroundDetail"
{
    Properties
    {
        [PerRendererData] _MainTex ("Original map", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _Strength ("Edge reconstruction", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="False" }
        Cull Off Lighting Off ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _MainTex_TexelSize, _Color;
            float _Strength;
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; float4 color:COLOR; };
            v2f vert(appdata v)
            { v2f o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv; o.color=v.color*_Color; return o; }
            float difference(float4 a,float4 b) { return max(length(a.rgb-b.rgb),abs(a.a-b.a)); }
            fixed4 frag(v2f i):SV_Target
            {
                float2 size=_MainTex_TexelSize.xy;
                float2 pixel=i.uv*_MainTex_TexelSize.zw;
                float2 uv=(floor(pixel)+.5)*size;
                fixed4 e=tex2D(_MainTex,uv);
                fixed4 b=tex2D(_MainTex,uv+float2(0,size.y));
                fixed4 h=tex2D(_MainTex,uv-float2(0,size.y));
                fixed4 d=tex2D(_MainTex,uv-float2(size.x,0));
                fixed4 f=tex2D(_MainTex,uv+float2(size.x,0));
                // Reconstruct diagonal corners using the original palette; never replace the map.
                fixed4 x=frac(pixel.x)<.5?d:f;
                fixed4 y=frac(pixel.y)<.5?h:b;
                float corner=step(.055,difference(b,h))*step(.055,difference(d,f));
                corner*=1-step(.025,difference(x,y));
                corner*=step(.99,e.a)*step(.99,x.a)*step(.99,y.a);
                float magnified=1-step(1,max(length(ddx(pixel)),length(ddy(pixel))));
                e.rgb=lerp(e.rgb,(x.rgb+y.rgb)*.5,corner*magnified*_Strength);
                return e*i.color;
            }
            ENDCG
        }
    }
}
