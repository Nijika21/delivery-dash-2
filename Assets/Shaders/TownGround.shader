// Tanah kota v7: satu quad yang membaca medan jarak hasil bake (Tools/town-baker).
// Warna dan pita sama dengan groundColor() di Tools/town-baker/lib/raster.mjs (pratinjau HTML dan minimap).
//   _Pave   : jarak ke aspal/pelataran (unit; negatif = di atas aspal). RHalf, atau RGBA8 terkuantisasi kalau _PaveQuant = 1.
//   _Ground : RGBA8 linear [kawasan, air, pelataran, nada]; air/pelataran = 128 + jarak*16.
Shader "Delivery Dash/Town Ground"
{
    Properties
    {
        _Pave ("Pavement SDF", 2D) = "white" {}
        _Ground ("Ground field", 2D) = "gray" {}
        _PaveQuant ("Pavement quantized", Float) = 0
        _Field ("Origin xy, cell, -", Vector) = (-150, -150, 0.25, 0)
        _FieldSize ("Samples xy", Vector) = (1201, 1201, 0, 0)
        _GrassA ("Grass A", Color) = (0.471, 0.639, 0.333, 1)
        _GrassB ("Grass B", Color) = (0.545, 0.706, 0.392, 1)
        _Water ("Water", Color) = (0.310, 0.608, 0.651, 1)
        _WaterEdge ("Water edge", Color) = (0.455, 0.702, 0.706, 1)
        _Sand ("Sand", Color) = (0.788, 0.757, 0.561, 1)
        _Asphalt ("Asphalt", Color) = (0.169, 0.180, 0.173, 1)
        _Paving ("Paving", Color) = (0.851, 0.859, 0.824, 1)
        _Seam ("Paving seam", Color) = (0.765, 0.773, 0.733, 1)
        _Walk ("Walk", Color) = (0.937, 0.914, 0.839, 1)
        _Curb ("Curb", Color) = (0.604, 0.608, 0.573, 1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "PreviewType"="Plane" }
        Cull Off
        ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionHCS : SV_POSITION; float2 world : TEXCOORD0; };

            TEXTURE2D(_Pave); SAMPLER(sampler_Pave);
            TEXTURE2D(_Ground); SAMPLER(sampler_Ground);
            CBUFFER_START(UnityPerMaterial)
                float _PaveQuant;
                float4 _Field, _FieldSize;
                half4 _GrassA, _GrassB, _Water, _WaterEdge, _Sand, _Asphalt, _Paving, _Seam, _Walk, _Curb;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 world = TransformObjectToWorld(input.positionOS.xyz);
                output.positionHCS = TransformWorldToHClip(world);
                output.world = world.xy;
                return output;
            }

            // 1 - smoothstep(e - aa, e + aa, x): 1 di sisi dalam pita, 0 di luar.
            float inside(float edge, float aa, float x) { return 1.0 - smoothstep(edge - aa, edge + aa, x); }

            half4 frag(Varyings input) : SV_Target
            {
                float2 p = input.world;
                float2 uv = ((p - _Field.xy) / _Field.z + 0.5) / _FieldSize.xy;
                float4 g = SAMPLE_TEXTURE2D(_Ground, sampler_Ground, uv);
                float paveRaw = SAMPLE_TEXTURE2D(_Pave, sampler_Pave, uv).r;
                float pave = _PaveQuant > 0.5 ? (paveRaw * 255.0 - 128.0) / 16.0 : paveRaw;
                float water = (g.g * 255.0 - 128.0) / 16.0;
                float plaza = (g.b * 255.0 - 128.0) / 16.0;
                float aa = max(fwidth(p.x), fwidth(p.y)) * 0.6;

                half3 c = lerp(_GrassA.rgb, _GrassB.rgb, g.a);
                c = lerp(c, _Sand.rgb, inside(0.35, aa, water) * 0.999);
                c = lerp(c, water > -0.7 ? _WaterEdge.rgb : _Water.rgb, inside(0.0, aa, water));
                c = lerp(c, _Curb.rgb, inside(0.48, aa, pave));
                c = lerp(c, _Walk.rgb, inside(0.22, aa, pave));

                half3 top = _Asphalt.rgb;
                float2 f = fmod(fmod(p, 1.333) + 1.333, 1.333);
                float seamDist = min(min(f.x, 1.333 - f.x), min(f.y, 1.333 - f.y));
                half3 paving = seamDist < 0.035 ? _Seam.rgb : _Paving.rgb;
                top = lerp(paving, _Asphalt.rgb, smoothstep(-aa, aa, plaza));
                c = lerp(c, top, inside(0.0, aa, pave));
                return half4(c, 1);
            }
            ENDHLSL
        }
    }
}
