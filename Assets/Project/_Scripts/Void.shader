Shader "Custom/Void"
{
    Properties
    {
        [Header(Palette)]
        _Color1 ("Base", Color) = (0.05, 0.02, 0.15, 1)
        _Color2 ("Mid", Color) = (0.4, 0.15, 0.6, 1)
        _Color3 ("Accent", Color) = (0.9, 0.4, 0.8, 1)
        
        [Header(Tint Filter)]
        _FilterColor ("Overlay Color (Alpha = Strength)", Color) = (0,0,0,0)
        
        [Header(Pixelation)]
        _Pixels ("Pixel Density", Float) = 400.0
        
        [Header(Spiral Motion)]
        _Speed ("Flow Speed", Float) = 0.4
        _SpiralStrength ("Spiral Twist", Range(0, 5)) = 2.0
        _FlowIterations ("Flow Complexity", Range(2, 8)) = 5
        
        [Header(Visual)]
        _FlowScale ("Pattern Scale", Float) = 8.0
        _Contrast ("Contrast", Range(0.5, 3)) = 1.8
        _Brightness ("Brightness", Range(0.5, 1.5)) = 1.0
        _EdgeSharpness ("Edge Sharpness", Range(1, 10)) = 3.0
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
                float _SpiralStrength;
                float _FlowIterations;
                float _FlowScale;
                float _Contrast;
                float _Brightness;
                float _EdgeSharpness;
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
                float time = _Time.y * _Speed;
                
                // === PIXELATION (применяем ПЕРВЫМ к исходным UV) ===
                float2 uv = IN.uv;
                uv = floor(uv * _Pixels) / _Pixels;
                
                // === SETUP & ASPECT CORRECTION ===
                float aspect = _ScreenParams.x / _ScreenParams.y;
                uv = uv - 0.5;
                uv.x *= aspect;
                
                // === POLAR COORDINATES WITH SPIRAL ===
                float dist = length(uv);
                float angle = atan2(uv.y, uv.x);
                
                // КЛЮЧЕВОЙ МОМЕНТ: Закручивание в центр
                // Чем ближе к центру, тем сильнее закручивание
                float spiral = _SpiralStrength * (1.0 - dist) * 5.0;
                angle += spiral + time * 0.15;
                
                // Восстанавливаем UV с закруткой
                uv = float2(cos(angle), sin(angle)) * dist;
                
                // === ITERATIVE FLOW SIMULATION ===
                float2 flowPos = uv * _FlowScale;
                
                int iterations = (int)_FlowIterations;
                for(int i = 0; i < iterations; i++)
                {
                    float fi = float(i + 1);
                    float t = time * 0.5;
                    
                    // Сложная органичная симуляция
                    flowPos += sin(flowPos.yx * 2.0 + fi + t) / fi;
                    flowPos.x += cos(flowPos.y * 1.3 + t * 0.7) * 0.5 / fi;
                    flowPos.y += sin(flowPos.x * 1.7 - t * 0.8) * 0.5 / fi;
                    
                    // Небольшое вращение для сложности
                    flowPos = mul(rot(time * 0.08 + fi * 0.15), flowPos);
                }
                
                // === CALCULATE PATTERN VALUE ===
                float pattern = length(flowPos) * 0.5;
                pattern = frac(pattern); // Повторяющиеся кольца
                
                // Добавляем волновую составляющую
                float wave = sin(flowPos.x * 0.5) * cos(flowPos.y * 0.5);
                pattern = pattern * 0.7 + wave * 0.3;
                
                // Нормализуем и применяем контраст
                pattern = clamp(pattern * 0.5 + 0.5, 0.0, 1.0);
                pattern = pow(pattern, 1.0 / _Contrast);
                
                // === COLOR MAPPING (3 COLORS) ===
                // Используем острые переходы для контрастности
                float edge = 1.0 / _EdgeSharpness;
                
                float mask1 = smoothstep(0.33 - edge, 0.33 + edge, pattern);
                float mask2 = smoothstep(0.66 - edge, 0.66 + edge, pattern);
                
                float4 col = _Color1;
                col = lerp(col, _Color2, mask1);
                col = lerp(col, _Color3, mask2);
                
                // === ЦЕНТРАЛЬНЫЙ GLOW ===
                // Добавляем свечение к центру для драматизма
                float centerGlow = exp(-dist * 3.0) * 0.3;
                col.rgb += _Color3.rgb * centerGlow;
                
                // === RADIAL LIGHTING ===
                // Мягкая подсветка по краям паттерна
                float lightEdge = max(0.0, pattern * 5.0 - 4.0);
                col.rgb += _Color3.rgb * lightEdge * 0.2;
                
                // === SOFT VIGNETTE ===
                float2 vignetteUV = IN.uv - 0.5;
                float vignette = 1.0 - dot(vignetteUV, vignetteUV) * 1.0;
                vignette = smoothstep(0.2, 1.0, vignette);
                col.rgb *= vignette;
                
                // === BREATHING ===
                // Легкая пульсация для живости
                float breathe = sin(time * 1.2) * 0.5 + 0.5;
                col.rgb *= (1.0 + breathe * 0.05);
                
                // === BRIGHTNESS ===
                col.rgb *= _Brightness;
                
                // === BASE COLOR BOOST ===
                // Немного добавляем базовый цвет чтобы не было чисто чёрных областей
                col.rgb = lerp(_Color1.rgb * 0.3, col.rgb, 0.85);
                
                // === SUBTLE DITHER ===
                float dither = frac(sin(dot(IN.uv, float2(12.9898, 78.233))) * 43758.5453);
                col.rgb += (dither - 0.5) * 0.02;
                
                // === TINT FILTER ===
                col.rgb = lerp(col.rgb, _FilterColor.rgb, _FilterColor.a);

                return col;
            }
            ENDHLSL
        }
    }
}