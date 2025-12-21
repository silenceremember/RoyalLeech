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

        // Стандартные параметры UI
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
        
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        
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
            // Shadow properties removed
            float _DistortStrength;
            float _Speed;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = v.texcoord;
                
                // Передаем цвет вершин (Градиент из TMP) без изменений
                OUT.color = v.color;
                return OUT;
            }

            // Функция SDF
            float GetSDF(float2 uv)
            {
                return tex2D(_MainTex, uv).a;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;
                
                // 1. Boil Effect (Искажение)
                float2 distort = float2(0,0);
                float time = _Time.y * _Speed;
                distort.x = sin(uv.y * 15.0 + time) * _DistortStrength;
                distort.y = cos(uv.x * 12.0 + time * 1.5) * _DistortStrength;
                
                float2 distortedUV = uv + distort;

                // 2. Пикселизация
                float2 pixelUV = floor(distortedUV * _Pixels) / _Pixels;

                // 3. Чтение (Shadow Removed)
                float d_text = GetSDF(pixelUV);
                float threshold = 0.5;

                // 4. Маски
                float isFace = step(threshold, d_text);

                // 5. Сборка Цвета
                float4 finalColor = float4(0,0,0,0);

                if (isFace > 0.5) 
                {
                    // Используем ТОЛЬКО цвет из TMP (Градиент)
                    finalColor = IN.color;
                }
                
                if (finalColor.a < 0.01) discard;

                finalColor.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                
                return finalColor;
            }
            ENDCG
        }
    }
}