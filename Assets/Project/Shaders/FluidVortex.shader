Shader "RoyalLeech/Background/FluidVortex"
{
    Properties
    {
        [Header(Palette)]
        _Color1 ("Deep (Background)", Color) = (0.1, 0.05, 0.2, 1)
        _Color2 ("Mid (Flow)", Color) = (0.5, 0.1, 0.7, 1)
        _Color3 ("High (Energy)", Color) = (0.2, 1.0, 0.8, 1)
        
        [Header(Tint Filter)]
        _FilterColor ("Overlay Color (Alpha = Strength)", Color) = (0,0,0,0)
        
        [Header(Pixelation)]
        _Pixels ("Pixel Density", Float) = 320.0
        
        [Header(Motion)]
        _Speed ("Time Speed", Float) = 0.5
        _SwirlStrength ("Swirl Power", Float) = 1.0
        _FlowScale ("Pattern Zoom", Float) = 3.0
        
        [Header(Sharpness and Balance)]
        // Убрали Range. Теперь вписывай любое число.
        _Hardness ("Sharpness (Raw Value)", Float) = 20.0
        _BandOffset ("Color Balance", Range(-1, 1)) = 0.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Background" "RenderPipeline" = "UniversalPipeline" }
        LOD 100

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float2 uv : TEXCOORD0;
                float4 positionHCS : SV_POSITION;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Color1;
                float4 _Color2;
                float4 _Color3;
                float4 _FilterColor;
                float _Pixels;
                float _Speed;
                float _SwirlStrength;
                float _FlowScale;
                float _Hardness;
                float _BandOffset;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            float2x2 rot(float a) 
            {
                float s = sin(a);
                float c = cos(a);
                return float2x2(c, -s, s, c);
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // 1. КОРРЕКЦИЯ
                float aspect = _ScreenParams.x / _ScreenParams.y;
                float2 uv = IN.uv - 0.5;
                uv.x *= aspect;
                
                float p = _Pixels;
                uv = floor(uv * p) / p; 

                float time = _Time.y * _Speed;
                float len = length(uv);

                // 2. ВИХРЬ
                float twist = log(len * 2.0 + 1.0);
                float angle = -time * 0.2 + twist * _SwirlStrength * 8.0;
                uv = mul(rot(angle), uv);

                // 3. СИМУЛЯЦИЯ
                uv *= _FlowScale;
                float2 fluidPos = uv;
                
                for(int i = 1; i < 4; i++) 
                {
                    float t = time * 0.5;
                    fluidPos.x += 0.6 / float(i) * sin(float(i) * 3.0 * fluidPos.y + t + 0.3 * float(i)) + 1.0;
                    fluidPos.y += 0.6 / float(i) * cos(float(i) * 3.0 * fluidPos.x + t + 0.3 * float(i * 10)) - 1.4;
                    fluidPos = mul(rot(time * 0.1), fluidPos);
                }

                // 4. ЦВЕТ
                float val = sin(fluidPos.x + fluidPos.y) * 0.5 + 0.5;
                
                // РАСЧЕТ РЕЗКОСТИ (Прямая формула)
                // Hardness 1 = blur 1.0 (Полное мыло)
                // Hardness 100 = blur 0.01 (Острая грань)
                // Hardness 1000 = blur 0.001 (Пиксельная бритва)
                float blur = 1.0 / max(_Hardness, 0.01); 
                
                float split1 = 0.33 + _BandOffset * 0.3;
                float split2 = 0.66 + _BandOffset * 0.3;

                float mask1 = smoothstep(split1 - blur, split1 + blur, val); 
                float mask2 = smoothstep(split2 - blur, split2 + blur, val); 

                float4 col = lerp(_Color1, _Color2, mask1);
                col = lerp(col, _Color3, mask2);

                // 5. ФИНАЛИЗАЦИЯ
                float dither = frac(sin(dot(IN.uv, float2(12.9898, 78.233))) * 43758.5453);
                col.rgb += (dither - 0.5) * 0.03;
                
                float2 dist = IN.uv - 0.5;
                float vignette = 1.0 - dot(dist, dist) * 1.5;
                col.rgb *= smoothstep(0.0, 1.0, vignette);
                
                // 6. TINT FILTER
                col.rgb = lerp(col.rgb, _FilterColor.rgb, _FilterColor.a);

                return col;
            }
            ENDHLSL
        }
    }
}