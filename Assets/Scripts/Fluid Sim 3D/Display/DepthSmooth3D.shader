Shader "Fluid/DepthSmooth3D"
{
    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass  // Pass 0 — horizontal
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f     { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = v.uv;
                return o;
            }

            sampler2D _MainTex;
            float4    _MainTex_TexelSize; // Unity fills this: xy = 1/width, 1/height
            float     _BlurRadius;        // how many pixels to sample each side
            float     _DepthFalloff;      // how strongly depth difference kills the weight

            float4 frag(v2f i) : SV_Target
            {
                float  centre    = tex2D(_MainTex, i.uv).r;
                if (centre > 1000) return centre; // background — skip

                float  weightSum = 0;
                float  depthSum  = 0;
                int    radius    = (int)_BlurRadius;

                for (int x = -radius; x <= radius; x++)
                {
                    float2 offset   = float2(_MainTex_TexelSize.x * x, 0);
                    float  sample   = tex2D(_MainTex, i.uv + offset).r;
                    if (sample > 1000) continue; // ignore background samples

                    // Gaussian spatial weight
                    float sigma     = radius / 3.0;
                    float spatialW  = exp(-(x*x) / (2*sigma*sigma));

                    // Depth similarity weight — far neighbors contribute less
                    float depthDiff = centre - sample;
                    float depthW    = exp(-(depthDiff*depthDiff) * _DepthFalloff);

                    float w = spatialW * depthW;
                    depthSum  += sample * w;
                    weightSum += w;
                }

                return weightSum > 0 ? depthSum / weightSum : centre;
            }
            ENDCG
        }

        Pass  // Pass 1 — vertical (identical, just y direction)
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f     { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv  = v.uv;
                return o;
            }

            sampler2D _MainTex;
            float4    _MainTex_TexelSize;
            float     _BlurRadius;
            float     _DepthFalloff;

            float4 frag(v2f i) : SV_Target
            {
                float  centre    = tex2D(_MainTex, i.uv).r;
                if (centre > 1000) return centre;

                float  weightSum = 0;
                float  depthSum  = 0;
                int    radius    = (int)_BlurRadius;

                for (int y = -radius; y <= radius; y++)
                {
                    float2 offset   = float2(0, _MainTex_TexelSize.y * y);
                    float  sample   = tex2D(_MainTex, i.uv + offset).r;
                    if (sample > 1000) continue;

                    float sigma     = radius / 3.0;
                    float spatialW  = exp(-(y*y) / (2*sigma*sigma));

                    float depthDiff = centre - sample;
                    float depthW    = exp(-(depthDiff*depthDiff) * _DepthFalloff);

                    float w = spatialW * depthW;
                    depthSum  += sample * w;
                    weightSum += w;
                }

                return weightSum > 0 ? depthSum / weightSum : centre;
            }
            ENDCG
        }
    }
}