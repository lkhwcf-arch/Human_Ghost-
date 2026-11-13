Shader "HumanGhost/Boss Phase Two Flash"
{
    Properties { [PerRendererData] _MainTex("Sprite Texture",2D)="white"{} _Color("Tint",Color)=(1,1,1,1) _FlashAmount("Hit Flash",Range(0,1))=0 _AuraStrength("Dark Red Aura",Range(0,1))=.18 }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma vertex Vertex
            #pragma fragment Fragment
            #pragma multi_compile_instancing
            #pragma multi_compile _ SKINNED_SPRITE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
            struct Attributes
            {
                float3 positionOS:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0;
                UNITY_SKINNED_VERTEX_INPUTS
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings { float4 positionCS:SV_POSITION; half4 color:COLOR; float2 uv:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            float4 _MainTex_TexelSize;
            CBUFFER_START(UnityPerMaterial)
                half4 _Color; half _FlashAmount; half _AuraStrength;
            CBUFFER_END
            Varyings Vertex(Attributes v)
            {
                Varyings o=(Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                UNITY_SKINNED_VERTEX_COMPUTE(v);
                SetUpSpriteInstanceProperties();
                v.positionOS=UnityFlipSprite(v.positionOS,unity_SpriteProps.xy);
                o.positionCS=TransformObjectToHClip(v.positionOS);o.color=v.color*_Color*unity_SpriteColor;o.uv=v.uv;return o;
            }
            half4 Fragment(Varyings i):SV_Target
            {
                half4 c=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv)*i.color;
                c.rgb=lerp(c.rgb,half3(1,1,1),_FlashAmount);
                half aura=0;
                for(int n=0;n<8;n++)
                {
                    float a=n*0.78539816;
                    aura+=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,i.uv+float2(cos(a),sin(a))*_MainTex_TexelSize.xy*2).a*.125h;
                }
                aura=max(0,aura-c.a)*_AuraStrength;
                half alpha=c.a+aura*(1-c.a);
                c.rgb=(c.rgb*c.a+half3(.35,0,.01)*aura*(1-c.a))/max(alpha,.0001h);
                c.a=alpha;return c;
            }
            ENDHLSL
        }
    }
}
