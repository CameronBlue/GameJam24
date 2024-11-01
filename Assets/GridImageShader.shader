Shader "Custom/GridImageShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _TextureTiling ("Texture Tiling", Vector) = (1,1,1,1)
        _Col0 ("Color 0", Color) = (1,1,1,1)
        _Tex0 ("Texture 0", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Lighting Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;

            float4 _TextureTiling;

            fixed4 _Col0;
            sampler2D _Tex0;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 texColor = tex2D(_MainTex, i.uv);
                float2 uv = (i.uv * _MainTex_TexelSize.zw / _TextureTiling.zw) % 1;
                fixed4 finalCol;
                
                if (dot(texColor - _Col0, texColor - _Col0) < 0.01)
                    finalCol = tex2D(_Tex0, uv);
                else
                    return texColor;                
                
                finalCol.a = texColor.a;                
                return finalCol;
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}