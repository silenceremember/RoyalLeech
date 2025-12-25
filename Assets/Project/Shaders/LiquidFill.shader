Shader "RoyalLeech/UI/LiquidFill"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
        
        [Header(Fill Effect)]
        _FillAmount ("Fill Amount", Range(0, 1)) = 1.0
        _FillColor ("Fill Color", Color) = (0.3, 0.7, 0.95, 1)
        _BackgroundColor ("Background Color", Color) = (0.1, 0.15, 0.25, 1)
        _BackgroundAlpha ("Background Alpha", Range(0, 1)) = 0.7
        _FillWaveStrength ("Wave Strength", Range(0, 0.1)) = 0.01
        _FillWaveSpeed ("Wave Speed", Float) = 2.0
        
        [Header(Liquid Effects)]
        _MeniscusStrength ("Meniscus", Range(0, 0.15)) = 0.04
        _LiquidTurbulence ("Turbulence", Range(0, 1)) = 0.0
        _BubbleIntensity ("Bubbles", Range(0, 1)) = 0.0
        _BubbleSize ("Bubble Size", Range(0.05, 0.2)) = 0.1
        _BubbleColor ("Bubble Color", Color) = (0.7, 0.9, 1.0, 0.8)
        _SplashIntensity ("Splash", Range(0, 1)) = 0.0
        
        [Header(Pixelation)]
        _PixelDensity ("Pixel Density (0=off)", Float) = 0
        
        [Header(Glow and Pulse)]
        _GlowColor ("Glow Color", Color) = (1, 0.8, 0.2, 1)
        _GlowIntensity ("Glow Intensity", Range(0, 2)) = 0.0
        _PulseSpeed ("Pulse Speed", Float) = 2.0
        _PulseIntensity ("Pulse Intensity", Range(0, 1)) = 0.0
        
        [Header(Shake Effect)]
        _ShakeIntensity ("Shake Intensity", Range(0, 20)) = 0.0
        _ShakeSpeed ("Shake Speed", Float) = 30.0
        
        [Header(Shadow)]
        [HideInInspector] _ShadowColor ("Shadow Color", Color) = (0, 0, 0, 0.5)
        
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }
    
    SubShader
    {
        Tags 
        { 
            "Queue"="Transparent" 
            "IgnoreProjector"="True" 
            "RenderType"="Transparent" 
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
            "RenderPipeline"="UniversalPipeline"
        }
        
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        
        Pass
        {
            Name "JuicyResourceIcon"
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 uv1 : TEXCOORD1;
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
                float isShadow : TEXCOORD1;
            };
            
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                
                float _FillAmount;
                float4 _FillColor;
                float4 _BackgroundColor;
                float _BackgroundAlpha;
                float _FillWaveStrength;
                float _FillWaveSpeed;
                
                float _MeniscusStrength;
                float _LiquidTurbulence;
                float _BubbleIntensity;
                float _BubbleSize;
                float4 _BubbleColor;
                float _SplashIntensity;
                float _PixelDensity;
                
                float4 _GlowColor;
                float _GlowIntensity;
                float _PulseSpeed;
                float _PulseIntensity;
                
                float _ShakeIntensity;
                float _ShakeSpeed;
                
                float4 _ShadowColor;
            CBUFFER_END
            
            float hash(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }
            
            // LIQUID SURFACE
            float getLiquidSurface(float2 uv, float time)
            {
                float surface = _FillAmount;
                
                // 1. MENISCUS - edge curve (always visible)
                float edgeDist = min(uv.x, 1.0 - uv.x);
                float meniscus = pow(1.0 - saturate(edgeDist * 2.5), 2.0) * _MeniscusStrength;
                surface += meniscus;
                
                // 2. BASE WAVE - gentle idle motion
                float wave = sin(uv.x * 8.0 + time * _FillWaveSpeed) * _FillWaveStrength;
                surface += wave;
                
                // 3. TURBULENCE - sharp fast waves (during shake)
                if (_LiquidTurbulence > 0.01)
                {
                    float t1 = sin(uv.x * 30.0 + time * 10.0) * _LiquidTurbulence * 0.025;
                    float t2 = sin(uv.x * 50.0 - time * 14.0) * _LiquidTurbulence * 0.018;
                    float t3 = cos(uv.x * 70.0 + time * 18.0) * _LiquidTurbulence * 0.012;
                    surface += t1 + t2 + t3;
                }
                
                // 4. SPLASH - central bump with spreading waves
                if (_SplashIntensity > 0.01)
                {
                    float centerDist = abs(uv.x - 0.5) * 2.0; // 0 at center, 1 at edges
                    
                    // BIG central splash bump
                    float bump = pow(1.0 - centerDist, 3.0) * _SplashIntensity * 0.15;
                    
                    // Waves spreading outward from center
                    float outwardWave = sin(centerDist * 20.0 - time * 10.0) * _SplashIntensity * 0.035;
                    outwardWave *= (1.0 - centerDist * 0.7); // Fade at edges
                    
                    // Waves bouncing back from edges
                    float inwardWave = sin((1.0 - centerDist) * 18.0 + time * 8.0) * _SplashIntensity * 0.02;
                    inwardWave *= centerDist; // Fade at center
                    
                    surface += bump + outwardWave + inwardWave;
                }
                
                return surface;
            }
            
            // BUBBLES
            float getBubbles(float2 uv, float fillLine, float time)
            {
                if (_BubbleIntensity < 0.01) return 0.0;
                if (uv.y > fillLine - 0.02) return 0.0;
                
                float bubbles = 0.0;
                
                // Big bubbles
                float2 buv1 = uv * (1.0 / _BubbleSize);
                buv1.y -= time * 0.5;
                buv1.x += sin(buv1.y * 2.0 + time) * 0.15;
                float b1 = hash(floor(buv1));
                float2 c1 = frac(buv1) - 0.5;
                bubbles += (1.0 - smoothstep(0.1, 0.35, length(c1))) * step(0.55, b1);
                
                // Medium bubbles  
                float2 buv2 = uv * (1.0 / (_BubbleSize * 0.6)) + 5.0;
                buv2.y -= time * 0.7;
                buv2.x += sin(buv2.y * 3.0 + time * 1.2) * 0.12;
                float b2 = hash(floor(buv2));
                float2 c2 = frac(buv2) - 0.5;
                bubbles += (1.0 - smoothstep(0.08, 0.28, length(c2))) * step(0.5, b2) * 0.6;
                
                return saturate(bubbles * _BubbleIntensity * 2.0);
            }
            
            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                float3 posOS = IN.positionOS.xyz;
                
                if (_ShakeIntensity > 0.001)
                {
                    float t = _Time.y * _ShakeSpeed;
                    posOS.x += sin(t) * cos(t * 1.3) * _ShakeIntensity;
                    posOS.y += cos(t * 0.7) * sin(t * 1.1) * _ShakeIntensity;
                }
                
                OUT.positionCS = TransformObjectToHClip(posOS);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color = IN.color * _Color;
                OUT.isShadow = IN.uv1.x;
                
                return OUT;
            }
            
            half4 frag(Varyings IN) : SV_Target
            {
                float time = _Time.y;
                float2 uv = IN.uv;
                float2 pixelUV = uv; // For pixelated calculations
                
                // Pixelation
                bool pixelated = _PixelDensity > 0.5;
                if (pixelated)
                {
                    pixelUV = floor(uv * _PixelDensity) / _PixelDensity;
                }
                
                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, pixelUV);
                
                if (IN.isShadow > 0.5)
                {
                    return half4(_ShadowColor.rgb, texColor.a * _ShadowColor.a * IN.color.a);
                }
                
                // Use pixelated UV for liquid calculations
                float fillLine = getLiquidSurface(pixelUV, time);
                
                // Pixelate the fill line itself for sharp edges
                if (pixelated)
                {
                    fillLine = floor(fillLine * _PixelDensity) / _PixelDensity;
                }
                
                // Sharp edge when pixelated, soft when not
                float isFilled;
                if (pixelated)
                {
                    isFilled = step(pixelUV.y, fillLine); // Sharp edge
                }
                else
                {
                    isFilled = smoothstep(fillLine + 0.012, fillLine - 0.012, uv.y);
                }
                
                half4 background = half4(_BackgroundColor.rgb, texColor.a * _BackgroundAlpha);
                half4 filled = texColor * _FillColor;
                
                // Bubbles with color
                float bubbles = getBubbles(pixelUV, fillLine, time);
                filled.rgb = lerp(filled.rgb, _BubbleColor.rgb, bubbles * _BubbleColor.a);
                
                // Surface glow line (skip when pixelated for cleaner look)
                if (!pixelated)
                {
                    float surfaceGlow = exp(-abs(uv.y - fillLine) * 60.0) * 0.4 * isFilled;
                    filled.rgb += surfaceGlow;
                }
                
                // Depth gradient
                filled.rgb *= lerp(0.85, 1.0, pixelUV.y / max(fillLine, 0.01));
                
                half4 result;
                result.rgb = lerp(background.rgb, filled.rgb, isFilled);
                result.a = lerp(background.a, filled.a, isFilled);
                
                // Glow (internal - does not extend beyond sprite bounds)
                if (_GlowIntensity > 0.001)
                {
                    // Simply add glow color to visible pixels
                    float glowMask = texColor.a;
                    glowMask *= 1.0 + sin(time * _PulseSpeed) * _PulseIntensity * 0.5;
                    result.rgb += _GlowColor.rgb * glowMask * _GlowIntensity * 0.5;
                }
                
                // Pulse (color brightness oscillation)
                if (_PulseIntensity > 0.001)
                    result.rgb *= 1.0 + sin(time * _PulseSpeed) * _PulseIntensity * 0.3;
                
                return result * IN.color;
            }
            ENDHLSL
        }
    }
    
    FallBack "UI/Default"
}
