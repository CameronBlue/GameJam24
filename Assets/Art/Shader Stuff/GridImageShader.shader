Shader "Custom/GridImageShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _TextureTiling ("Texture Tiling", Vector) = (1,1,1,1)
        
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

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;

            float4 _TextureTiling;

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

            float2 uv_offset(float _f)
            {
                return float2(_SinTime.y, _CosTime.y * 0.5) * _f * _f;
            }

            float shimmer(float2 uv)
            {
                float2 scaledUV = uv * 100;
                float positionalNoise = sin(scaledUV.x) + sin(scaledUV.y);
                float shimmer = 0.5 + 0.5 * sin(_Time.y * (2 + positionalNoise * 0.25));
                return 5 * shimmer;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float4 c = tex2D(_MainTex, i.uv);
                int type = (c.x * 255.0);
                int neighbours = (c.y * 255.0); //0-15
                float viscosity = c.z;

                float2 uv = (uv_offset(1 - viscosity) + i.uv * _MainTex_TexelSize.zw / _TextureTiling.zw) % 1;
                uv += float2(3 - (neighbours >> 2),  neighbours & 3);
                uv *= 0.25;
                
                fixed4 finalCol;
                if (type == 1)
                    finalCol = tex2D(_Tex0, uv);
                else if (type == 2)
                    finalCol = tex2D(_Tex1, uv);
                else if (type == 3)
                    finalCol = tex2D(_Tex2, uv);
                else if (type == 4)
                    finalCol = tex2D(_Tex3, uv);
                else
                    finalCol = fixed4(c.rgb, 1);
                
                finalCol.a *= c.a;
                finalCol.xyz *= 1 + (1 - viscosity);
                return finalCol;
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}