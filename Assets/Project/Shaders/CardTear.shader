Shader "RoyalLeech/UI/CardTear"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        
        [Header(Tear Settings)]
        _TearDepth ("Max Tear Depth", Range(0, 0.4)) = 0.15
        _TearSeed ("Random Seed", Float) = 0
        
        [Header(Teeth Settings)]
        _TeethCount ("Max Teeth Per Edge", Range(1, 8)) = 3
        _TeethMinWidth ("Min Width", Range(0.02, 0.2)) = 0.05
        _TeethMaxWidth ("Max Width", Range(0.05, 0.4)) = 0.15
        
        [Header(Animation)]
        _AnimSpeed ("Frames Per Second", Float) = 1.0
        
        [Header(Per Edge Intensity)]
        _TopTear ("Top Edge", Range(0, 1)) = 0.5
        _BottomTear ("Bottom Edge", Range(0, 1)) = 0.5
        _LeftTear ("Left Edge", Range(0, 1)) = 0.5
        _RightTear ("Right Edge", Range(0, 1)) = 0.5
        
        [Header(Corner Cut)]
        _CornerCutMin ("Corner Cut Min", Range(0, 0.2)) = 0.05
        _CornerCutMax ("Corner Cut Max", Range(0, 0.3)) = 0.12
        
        [Header(Stencil)]
        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
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
        Blend One OneMinusSrcAlpha
        ColorMask [_ColorMask]
        
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float _TearDepth;
                float _TearSeed;
                float _TeethCount;
                float _TeethMinWidth;
                float _TeethMaxWidth;
                float _AnimSpeed;
                float _TopTear;
                float _BottomTear;
                float _LeftTear;
                float _RightTear;
                float _CornerCutMin;
                float _CornerCutMax;
            CBUFFER_END
            
            // Hash functions
            float Hash(float n)
            {
                return frac(sin(n * 127.1) * 43758.5453);
            }
            
            float Hash2(float2 p)
            {
                return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
            }
            
            // Check if point is inside a triangle notch coming from edge
            // edgePos: position along edge (0-1)
            // distFromEdge: how far from edge (0 at edge, positive going inward)
            // toothIndex: which tooth we're checking
            // seed: random seed
            // Returns 1 if inside notch (should be cut), 0 if outside
            float IsInsideToothNotch(float edgePos, float distFromEdge, float toothIndex, float seed,
                                      float minWidth, float maxWidth, float maxDepth)
            {
                // Random parameters for this tooth
                float r1 = Hash(toothIndex + seed);
                float r2 = Hash(toothIndex + seed + 100.0);
                float r3 = Hash(toothIndex + seed + 200.0);
                float r4 = Hash(toothIndex + seed + 300.0);
                float r5 = Hash(toothIndex + seed + 400.0);
                
                // Should this tooth exist? (~60% chance)
                if (r1 < 0.4) return 0.0;
                
                // Tooth center position along edge
                float toothCenter = (toothIndex + 0.5 + (r2 - 0.5) * 0.6) / _TeethCount;
                
                // Random width and depth
                float toothWidth = lerp(minWidth, maxWidth, r3);
                float toothDepth = maxDepth * (0.4 + r4 * 0.6); // 40-100% of max depth
                
                // Asymmetry - tip offset from center (-0.3 to 0.3 of width)
                float tipOffset = (r5 - 0.5) * 0.6 * toothWidth;
                
                // Triangle vertices:
                // Base left:  (toothCenter - toothWidth/2, 0)
                // Base right: (toothCenter + toothWidth/2, 0)  
                // Tip:        (toothCenter + tipOffset, toothDepth)
                
                float baseLeft = toothCenter - toothWidth * 0.5;
                float baseRight = toothCenter + toothWidth * 0.5;
                float tipX = toothCenter + tipOffset;
                float tipY = toothDepth;
                
                // Check if edgePos is within the horizontal range of the triangle
                if (edgePos < baseLeft || edgePos > baseRight) return 0.0;
                
                // For a given edgePos, calculate the max depth at that position
                // using linear interpolation between the two edges and tip
                float maxDepthAtPos;
                
                if (edgePos < tipX)
                {
                    // Left side of triangle
                    float t = (edgePos - baseLeft) / max(tipX - baseLeft, 0.001);
                    maxDepthAtPos = t * tipY;
                }
                else
                {
                    // Right side of triangle
                    float t = (edgePos - tipX) / max(baseRight - tipX, 0.001);
                    maxDepthAtPos = (1.0 - t) * tipY;
                }
                
                // If our distance from edge is less than the triangle depth at this position,
                // we're inside the notch
                return step(distFromEdge, maxDepthAtPos);
            }
            
            // Calculate total tear for an edge
            // Returns 1 if pixel should be cut (inside any notch), 0 otherwise
            float CalculateEdgeTear(float edgePos, float distFromEdge, float seed, float intensity)
            {
                if (intensity < 0.01) return 0.0;
                
                float result = 0.0;
                
                // Check each potential tooth
                for (int i = 0; i < 8; i++)
                {
                    if (float(i) >= _TeethCount) break;
                    
                    result = max(result, IsInsideToothNotch(
                        edgePos, 
                        distFromEdge, 
                        float(i), 
                        seed,
                        _TeethMinWidth,
                        _TeethMaxWidth,
                        _TearDepth * intensity
                    ));
                }
                
                return result;
            }
            
            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color = IN.color * _Color;
                
                return OUT;
            }
            
            half4 frag(Varyings IN) : SV_Target
            {
                // Stepped time - changes once per frame at _AnimSpeed FPS
                float steppedTime = floor(_Time.y * _AnimSpeed);
                float seed = _TearSeed + steppedTime * 17.31;
                
                // Sample sprite
                half4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                color *= IN.color;
                
                // Distance from each edge
                float distTop = 1.0 - IN.uv.y;
                float distBottom = IN.uv.y;
                float distLeft = IN.uv.x;
                float distRight = 1.0 - IN.uv.x;
                
                // Check each edge for tears
                float tearTop = CalculateEdgeTear(IN.uv.x, distTop, seed, _TopTear);
                float tearBottom = CalculateEdgeTear(IN.uv.x, distBottom, seed + 1000.0, _BottomTear);
                float tearLeft = CalculateEdgeTear(IN.uv.y, distLeft, seed + 2000.0, _LeftTear);
                float tearRight = CalculateEdgeTear(IN.uv.y, distRight, seed + 3000.0, _RightTear);
                
                // Combine tears - if any edge says cut, cut
                float shouldCut = max(max(tearTop, tearBottom), max(tearLeft, tearRight));
                
                // Corner cut - diagonal cuts at each corner with random size
                // For each corner, check if we're within the cut zone
                float cornerMask = 1.0;
                
                // Random cut size per corner (changes with animation)
                float tlCut = lerp(_CornerCutMin, _CornerCutMax, Hash(seed + 500.0));
                float trCut = lerp(_CornerCutMin, _CornerCutMax, Hash(seed + 600.0));
                float blCut = lerp(_CornerCutMin, _CornerCutMax, Hash(seed + 700.0));
                float brCut = lerp(_CornerCutMin, _CornerCutMax, Hash(seed + 800.0));
                
                // Top-left corner
                float tlDist = distLeft + distTop;
                cornerMask *= step(tlCut, tlDist);
                
                // Top-right corner
                float trDist = distRight + distTop;
                cornerMask *= step(trCut, trDist);
                
                // Bottom-left corner
                float blDist = distLeft + distBottom;
                cornerMask *= step(blCut, blDist);
                
                // Bottom-right corner
                float brDist = distRight + distBottom;
                cornerMask *= step(brCut, brDist);
                
                // Apply mask (tears + corner cuts)
                color.a *= (1.0 - shouldCut) * cornerMask;
                
                // Premultiply alpha
                color.rgb *= color.a;
                
                return color;
            }
            ENDHLSL
        }
    }
    
    Fallback "Sprites/Default"
}
