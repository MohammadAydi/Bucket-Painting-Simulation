Shader "Fluid/ParticleCircle"
{
    Properties
    {
        _Color          ("Color",          Color)       = (1,1,1,1)
        _Smoothness     ("Smoothness",     Range(0,1))  = 0.0
        _SmoothingRadius("Smoothing Radius", Float)     = 1.0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha One
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            float  _Smoothness;
            float  _SmoothingRadius;

            struct appdata
            {
                float4 vertex : POSITION;
                float4 color  : COLOR;
            };

            struct v2f
            {
                float4 pos   : SV_POSITION;
                fixed4 color : COLOR;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos   = UnityObjectToClipPos(v.vertex);
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // dst is normalized (0=center, 1=edge=smoothingRadius)
                // convert back to real distance
                float dst    = i.color.a * _SmoothingRadius;
                float radius = _SmoothingRadius;

                // Exact kernel from pseudocode:
                // volume = PI * radius^8 / 4
                // value  = max(0, radius² - dst²)
                // return value³ / volume
                float volume = UNITY_PI * pow(radius, 8) / 4.0;
                float value  = max(0.0, radius * radius - dst * dst);
                float kernel = (value * value * value) / volume;

                // Hard circle (smoothness = 0)
                float hardEdge = 1.0 - step(1.0, i.color.a);

                // kernel gives absolute density contribution — normalize to 0..1 for alpha
                // max possible value is when dst=0: radius^6 / volume = 4 / (PI * radius^2)
                float maxKernel = 4.0 / (UNITY_PI * radius * radius);
                float kernelNorm = kernel / maxKernel;

                float alpha = lerp(hardEdge, kernelNorm, _Smoothness);
                return fixed4(i.color.rgb, alpha);
            }
            ENDCG
        }
    }
}