Shader "HumanGhost/Player Silhouette"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _BlurPixels ("Edge Blur Pixels", Range(0, 3)) = 1.25
        [HideInInspector] _SpriteUVRect ("Sprite UV Rect", Vector) = (0, 0, 1, 1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize;
            CBUFFER_START(UnityPerMaterial)
                float _BlurPixels;
                float4 _SpriteUVRect;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half alpha : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                SetUpSpriteInstanceProperties();
                output.positionCS = TransformObjectToHClip(UnityFlipSprite(input.positionOS, unity_SpriteProps.xy));
                output.uv = input.uv;
                output.alpha = input.color.a * unity_SpriteColor.a;
                return output;
            }
            half Alpha(float2 uv)
            {
                // 인접 스프라이트 셀의 그림이 번지지 않게 현재 셀 안에서만 읽습니다.
                float2 inset = _MainTex_TexelSize.xy * 0.5;
                float2 bounded = clamp(uv, _SpriteUVRect.xy + inset, _SpriteUVRect.zw - inset);
                half inside = all(uv >= _SpriteUVRect.xy) && all(uv <= _SpriteUVRect.zw);
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, bounded).a * inside;
            }
            half4 Fragment(Varyings input) : SV_Target
            {
                float2 d = _MainTex_TexelSize.xy * _BlurPixels;
                half alpha = 4 * Alpha(input.uv);
                alpha += 2 * (Alpha(input.uv + float2(d.x, 0)) + Alpha(input.uv - float2(d.x, 0))
                    + Alpha(input.uv + float2(0, d.y)) + Alpha(input.uv - float2(0, d.y)));
                alpha += Alpha(input.uv + d) + Alpha(input.uv - d)
                    + Alpha(input.uv + float2(d.x, -d.y)) + Alpha(input.uv + float2(-d.x, d.y));
                return half4(0, 0, 0, alpha * input.alpha / 16);
            }
            ENDHLSL
        }
    }
}
