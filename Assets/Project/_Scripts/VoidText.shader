Shader "UI/VoidText"
{
    Properties
    {
        [Header(Texture)]
        _MainTex ("Font Texture", 2D) = "white" {}
        
        [Header(Pixelation)]
        _Pixels ("Pixel Density", Float) = 100.0
        
        [Header(Boil Effect)]
        _DistortStrength ("Boil Strength", Range(0, 0.05)) = 0.005
        _Speed ("Boil Speed", Float) = 5.0

        // Standard UI Stencil Properties
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
     
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" }
        
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

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _ClipRect;
            float _Pixels;
            float _DistortStrength;
            float _Speed;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                
                OUT.worldPosition = mul(unity_ObjectToWorld, v.vertex);
                
                OUT.vertex = UnityObjectToClipPos(v.vertex);
                OUT.texcoord = v.texcoord;
                OUT.color = v.color;
                
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float time = _Time.y * _Speed;
                
                float2 distort;
                distort.x = sin(IN.texcoord.y * 15.0 + time);
                distort.y = cos(IN.texcoord.x * 12.0 + time * 1.5);
                
                float2 distortedUV = IN.texcoord + (distort * _DistortStrength);

                float2 pixelUV = floor(distortedUV * _Pixels) / _Pixels;

                float d_text = tex2D(_MainTex, pixelUV).a;
                
                float mask = step(0.5, d_text); 
                
                float4 finalColor = IN.color * mask;

                #ifdef UNITY_UI_CLIP_RECT
                finalColor.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                clip(finalColor.a - 0.001);

                return finalColor;
            }
            ENDCG
        }
    }
}
