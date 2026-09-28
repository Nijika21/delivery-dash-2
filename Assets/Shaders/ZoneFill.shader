// Zona berhenti: tepi terisi melingkar sesuai progres (gerak "zona-isi").
// _MainTex = zona dasar (dari sprite), _FullTex = zona dengan tepi penuh, ukuran dan tata letak sama.
// _Progress = pecahan sudut 0..1 dari atas searah jarum jam (StopZoneView mengubah progres panjang tepi ke sudut).
// _Aspect = ukuran piksel gambar, supaya sudut dihitung di ruang piksel (kapsul tidak persegi).
Shader "Delivery Dash/Zone Fill"
{
    Properties
    {
        [PerRendererData] _MainTex ("Base", 2D) = "white" {}
        _FullTex ("Full", 2D) = "white" {}
        _Progress ("Progress", Range(0, 1)) = 0
        _Aspect ("Pixel size", Vector) = (256, 256, 0, 0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" "PreviewType"="Plane" "CanUseSpriteAtlas"="False" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_FullTex); SAMPLER(sampler_FullTex);
            CBUFFER_START(UnityPerMaterial)
                float _Progress;
                float4 _Aspect;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                // Unity 6 URP: SpriteRenderer.color (kedip, muncul, hilang) dikirim lewat unity_SpriteColor, bukan warna
                // verteks. Sama seperti Sprite-Unlit-Default: warna verteks × unity_SpriteColor.
                output.color = input.color * unity_SpriteColor;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 d = (input.uv - 0.5) * _Aspect.xy;
                float angle = atan2(d.x, d.y) / 6.2831853;
                angle = angle < 0 ? angle + 1 : angle;
                half4 baseColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half4 fullColor = SAMPLE_TEXTURE2D(_FullTex, sampler_FullTex, input.uv);
                half4 c = angle < _Progress ? fullColor : baseColor;
                return c * input.color;
            }
            ENDHLSL
        }
    }
}
