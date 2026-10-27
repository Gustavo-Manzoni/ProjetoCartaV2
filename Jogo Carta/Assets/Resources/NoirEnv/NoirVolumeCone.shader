// Cone de luz volumetrico falso para as lampadas das salas (NoirEnvironment.Sinister).
// Aditivo, mas com tres atenuacoes para nao parecer um vidro colorido iluminando o fundo:
//  - borda: o brilho cresce com o caminho dentro do cone (centro forte, contorno sumindo);
//  - superficie: some suave onde encosta no chao, parede ou movel (depth texture);
//  - camera: some quando o jogador entra no cone ou chega perto.
Shader "ProjetoCarta/NoirVolumeCone"
{
    Properties
    {
        _Color ("Cor (alfa = forca)", Color) = (1, 0.8, 0.5, 0.05)
        _Rim ("Borda", Float) = 2.2
        _Soft ("Suavizar nas superficies (m)", Float) = 0.8
        _NearFade ("Sumir perto da camera (m)", Float) = 1.6
        _Falloff ("Queda ao longo do cone", Float) = 1.4
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float _Rim;
                float _Soft;
                float _NearFade;
                float _Falloff;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float4 screenPos : TEXCOORD2;
                float along : TEXCOORD3;
            };

            Varyings vert(Attributes input)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.screenPos = ComputeScreenPos(p.positionCS);
                o.along = input.uv.y;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 toCam = GetCameraPositionWS() - i.positionWS;
                float camDist = length(toCam);
                float facing = abs(dot(normalize(i.normalWS), toCam / max(camDist, 1e-4)));
                float rim = pow(saturate(facing), _Rim);
                float along = pow(saturate(1.0 - i.along), _Falloff);

                float2 uv = i.screenPos.xy / i.screenPos.w;
                float sceneZ = LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
                float soft = saturate((sceneZ - i.screenPos.w) / _Soft);

                float nearFade = saturate((camDist - 0.4) / _NearFade);
                float a = _Color.a * rim * along * soft * nearFade;
                return half4(_Color.rgb * a, 0);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
