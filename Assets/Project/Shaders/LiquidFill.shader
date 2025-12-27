Shader "RoyalLeech/UI/LiquidFill"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [HideInInspector] _Color ("Tint", Color) = (1,1,1,1)
        
        [Header(Fill Effect)]
        _FillAmount ("Fill Amount", Range(0, 1)) = 1.0
        _TrailingFill ("Trailing Fill (delayed)", Range(0, 1)) = 1.0
        _FillColor ("Fill Color", Color) = (0.3, 0.7, 0.95, 1)
        _BackgroundColor ("Background Color", Color) = (0.1, 0.15, 0.25, 1)
        _BackgroundAlpha ("Background Blend (0=black, 1=color)", Range(0, 1)) = 0.7
        _FillWaveStrength ("Wave Strength", Range(0, 0.1)) = 0.01
        _FillWaveSpeed ("Wave Speed", Float) = 2.0
        
        [Header(Liquid Effects)]
        _MeniscusStrength ("Meniscus", Range(0, 0.15)) = 0.04
        _LiquidTurbulence ("Turbulence", Range(0, 1)) = 0.0
        _BubbleIntensity ("Bubble Intensity", Range(0, 1)) = 0.0
        _BubbleSize ("Bubble Size", Range(0.02, 0.25)) = 0.08
        _BubbleDensity ("Bubble Density", Range(0, 1)) = 0.4
        _BubbleSpeed ("Bubble Speed", Range(0.1, 2)) = 0.6
        _BubblePixelation ("Bubble Pixelation (0=smooth)", Float) = 0
        _SplashIntensity ("Splash", Range(0, 1)) = 0.0
        
        [Header(Pixelation)]
        _PixelDensity ("Pixel Density (0=off)", Float) = 0
        
        [Header(Effects)]
        _EffectIntensity ("Effect Intensity (-1=decrease, +1=increase)", Range(-1, 1)) = 0.0
        _IncreaseStrength ("Increase Strength (lighten)", Range(0, 2)) = 0.5
        _DecreaseStrength ("Decrease Strength (darken)", Range(0, 2)) = 0.5
        _GlowIntensity ("Glow Intensity (pulsing darken)", Range(0, 2)) = 0.0
        _GlowStrength ("Glow Strength", Range(0, 2)) = 0.5
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
                float _TrailingFill;
                float4 _FillColor;
                float4 _BackgroundColor;
                float _BackgroundAlpha;
                float _FillWaveStrength;
                float _FillWaveSpeed;
                
                float _MeniscusStrength;
                float _LiquidTurbulence;
                float _BubbleIntensity;
                float _BubbleSize;
                float _BubbleDensity;
                float _BubbleSpeed;
                float _BubblePixelation;
                float _SplashIntensity;
                float _PixelDensity;
                
                float _EffectIntensity;
                float _IncreaseStrength;
                float _DecreaseStrength;
                float _GlowIntensity;
                float _GlowStrength;
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
            
            // BOILING BUBBLES - Single layer with realistic physics
            // Bubbles rise from bottom, wobble naturally, pop near surface
            // Separate pixelation control
            float getBoilingBubbles(float2 uv, float time, float seedOffset, float2 originalUV)
            {
                // Apply bubble-specific pixelation if set
                float2 bubbleUV = uv;
                if (_BubblePixelation > 0)
                {
                    bubbleUV = floor(uv * _BubblePixelation) / _BubblePixelation;
                }
                
                float bubbles = 0.0;
                float intensity = _BubbleIntensity;
                
                // Opacity controlled by intensity - very gradual fade in
                float opacity = smoothstep(0.0, 0.5, intensity);
                if (opacity < 0.01) return 0.0;
                
                // Edge fade - bubbles fade near sprite edges
                float edgeX = 1.0 - abs(originalUV.x - 0.5) * 2.0;
                float edgeY = 1.0 - abs(originalUV.y - 0.5) * 2.0;
                float edgeFade = saturate(min(edgeX, edgeY) * 3.5);
                
                // Surface fade - bubbles "pop" near top (realistic boiling)
                // 0 at top (y=1), 1 at bottom (y=0)
                float surfaceFade = saturate((1.0 - originalUV.y) * 1.8);
                
                // Grid scale based on bubble size
                float scale = 1.0 / _BubbleSize;
                
                // Bubble grid
                float2 buv = bubbleUV * scale + seedOffset;
                buv.y -= time * _BubbleSpeed; // Rising motion
                
                // Natural wobble as bubbles rise
                float wobblePhase = buv.y * 0.4 + time * 0.5 + seedOffset;
                buv.x += sin(wobblePhase) * 0.25;
                
                // Per-cell calculations
                float2 cell = floor(buv);
                float h = hash(cell + seedOffset);
                float h2 = hash(cell + 0.5 + seedOffset);
                float h3 = hash(cell + 0.9 + seedOffset);
                
                // Position within cell (random offset for variety)
                float2 c = frac(buv) - 0.5;
                c += (float2(h2, h3) - 0.5) * 0.4;
                
                // Random size variation per bubble
                float sizeVar = 0.7 + h * 0.6; // 0.7 to 1.3x
                float radius = 0.35 * sizeVar;
                
                // Visibility based on density
                float visible = step(1.0 - _BubbleDensity, h);
                
                // Circle check
                float dist = length(c);
                float bubble = step(dist, radius) * visible;
                
                bubbles = bubble * surfaceFade;
                
                return saturate(bubbles) * edgeFade * opacity;
            }
            
            // Separate patterns for filled and background areas
            float getBubblesFilled(float2 uv, float time, float2 originalUV)
            {
                return getBoilingBubbles(uv, time, 0.0, originalUV);
            }
            
            float getBubblesBackground(float2 uv, float time, float2 originalUV)
            {
                return getBoilingBubbles(uv, time, 50.0, originalUV);
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
                float2 pixelUV = uv; // For pixelated liquid calculations (waves, bubbles, edge)
                
                // Pixelation
                bool pixelated = _PixelDensity > 0.5;
                if (pixelated)
                {
                    pixelUV = floor(uv * _PixelDensity) / _PixelDensity;
                }
                
                // Sample texture from pixelUV for pixelated icon, gradient will use original uv
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
                
                // === TRAILING FILL (works for both increase and decrease) ===
                // Instead of calculating trailing surface separately, use fillLine + offset
                // This ensures PERFECT wave sync (no floating point mismatch)
                float trailingOffset = _TrailingFill - _FillAmount;
                float trailingLine = fillLine + trailingOffset;
                
                // isTrailingFilled = using trailing level instead of fill level
                float isTrailingFilled;
                if (pixelated)
                {
                    isTrailingFilled = step(pixelUV.y, trailingLine);
                }
                else
                {
                    isTrailingFilled = smoothstep(trailingLine + 0.012, trailingLine - 0.012, uv.y);
                }
                
                // Trailing zone = XOR between filled and trailing filled
                // Loss: trailing > fill → shows area above fill, below trailing
                // Gain: trailing < fill → shows area above trailing, below fill
                float isTrailing = abs(isTrailingFilled - isFilled);
                
                // Background and Filled colors
                half3 bgColor = _BackgroundColor.rgb * _BackgroundAlpha;
                half4 background = half4(bgColor, texColor.a);
                half4 filled = texColor * _FillColor;
                
                // Bubbles - SEPARATE patterns for filled and background areas
                float bubblesFilled = getBubblesFilled(pixelUV, time, uv);
                float bubblesBackground = getBubblesBackground(pixelUV, time, uv);
                // Filled: lighter bubbles. Background: DARKER bubbles (distinct separation)
                half3 filledBubbleColor = filled.rgb + 0.18;
                half3 bgBubbleColor = bgColor * 0.6; // Darker, not lighter
                filled.rgb = lerp(filled.rgb, filledBubbleColor, bubblesFilled);
                background.rgb = lerp(background.rgb, bgBubbleColor, bubblesBackground);
                
                // TRAILING: solid light color (bright version of fill)
                half3 trailingColor = filled.rgb + 0.25; // Lighter version
                
                // Blend layers: background → filled → trailing (ON TOP!)
                // Trailing is drawn LAST so it's visible for both increase and decrease
                half4 result;
                result.rgb = background.rgb;
                result.rgb = lerp(result.rgb, filled.rgb, isFilled);
                result.rgb = lerp(result.rgb, trailingColor, isTrailing); // Trailing on top!
                result.a = texColor.a;
                
                // === Effects: Simple Light/Dark Overlay ===
                float mask = texColor.a;
                
                // Glow: pulsing DARKEN overlay (works like effect: intensity × strength)
                if (_GlowIntensity > 0.0001)
                {
                    float pulse = 0.5 + sin(time * _PulseSpeed) * 0.5; // 0 to 1 pulse
                    float glowAmount = _GlowIntensity * _GlowStrength * mask;
                    // Lerp towards darker, pulse modulates target darkness
                    half3 darkColor = result.rgb * lerp(0.5, 0.2, pulse);
                    result.rgb = lerp(result.rgb, darkColor, glowAmount);
                }
                
                // Effect: LIGHTEN (positive) or DARKEN (negative) overlay
                if (abs(_EffectIntensity) > 0.0001)
                {
                    if (_EffectIntensity > 0)
                    {
                        // Positive: lerp towards white (lighten)
                        float increaseAmount = _EffectIntensity * _IncreaseStrength * mask;
                        result.rgb = lerp(result.rgb, half3(1, 1, 1), increaseAmount);
                    }
                    else
                    {
                        // Negative: lerp towards black (darken)
                        float decreaseAmount = abs(_EffectIntensity) * _DecreaseStrength * mask;
                        result.rgb = lerp(result.rgb, half3(0, 0, 0), decreaseAmount);
                    }
                }
                
                // Pulse (overall brightness oscillation when no glow)
                if (_PulseIntensity > 0.001 && _GlowIntensity < 0.001)
                    result.rgb *= 1.0 + sin(time * _PulseSpeed) * _PulseIntensity * 0.3;
                
                return result * IN.color;
            }
            ENDHLSL
        }
    }
    
    FallBack "UI/Default"
}
