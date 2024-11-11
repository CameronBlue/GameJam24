Shader "Custom/BackgroundShader"
{
    Properties
    {
        _TextureTiling ("Texture Tiling", Vector) = (1,1,1,1)
        _Parallax ("Parallax", Vector) = (0,0,0,0)
        _Seed ("Seed", Float) = 0
        
        _Tex0 ("Texture 0", 2D) = "white" {}
        _Tex1 ("Texture 1", 2D) = "white" {}
        _Tex2 ("Texture 2", 2D) = "white" {}
        _Tex3 ("Texture 3", 2D) = "white" {}
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

            float4 _Parallax;
            float4 _TextureTiling;
            float _Seed;
            sampler2D _Tex0;
            sampler2D _Tex1;
            sampler2D _Tex2;
            sampler2D _Tex3;

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

            float hash_to_01(int2 _index)
            {
                int hash = _index.x * 73856093 ^ _index.y * 19349663;
                return (abs(hash / 100000.0) + _Seed) % 1;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 uv = i.uv * _TextureTiling.xy + _Parallax.xy;
                int2 cellIndex = int2(uv);
                uv -= cellIndex;

                float rand = hash_to_01(cellIndex);
                fixed4 final_col;
                if (rand < 0.6)
                    final_col = tex2D(_Tex0, uv);
                else if (rand < 0.997)
                    final_col = tex2D(_Tex1, uv);
                else if (rand < 0.999)
                    final_col = tex2D(_Tex2, uv);
                else
                    final_col = tex2D(_Tex3, uv);
                return final_col;
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}