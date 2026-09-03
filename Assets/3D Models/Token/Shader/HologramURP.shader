Shader "Custom/HologramURP_DoubleSided"
{
    Properties
    {
        [MainColor]
        _HologramColor ("Hologram Color", Color) = (0.0, 0.8, 1.0, 1.0)

        [MainTexture]
        _MainTex ("Hologram Image", 2D) = "white" {}

        _ImageIntensity ("Image Intensity", Range(0, 5)) = 1.0

        [Header(Glow)]
        _GlowStrength ("Glow Strength", Range(0, 10)) = 2.5
        _Transparency ("Transparency", Range(0, 1)) = 0.65

        [Header(Bottom To Top Fade)]
        _GradientBottom ("Gradient Bottom", Range(0, 1)) = 0.0
        _GradientTop ("Gradient Top", Range(0, 1)) = 1.0
        _GradientPower ("Gradient Power", Range(0.1, 10)) = 1.5

        [Header(Fresnel)]
        _FresnelPower ("Fresnel Power", Range(0.1, 10)) = 3.0
        _FresnelStrength ("Fresnel Strength", Range(0, 10)) = 3.0

        [Header(Scanlines)]
        _ScanlineIntensity ("Scanline Intensity", Range(0, 1)) = 0.25
        _ScanlineScale ("Scanline Scale", Range(1, 300)) = 80
        _ScanlineSpeed ("Scanline Speed", Range(-20, 20)) = 3

        [Header(Flicker)]
        _FlickerStrength ("Flicker Strength", Range(0, 1)) = 0.15
        _FlickerSpeed ("Flicker Speed", Range(0, 30)) = 8

        [Header(Distortion)]
        _DistortionStrength ("Distortion Strength", Range(0, 1)) = 0.05
        _DistortionSpeed ("Distortion Speed", Range(0, 20)) = 4
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        // ============================================
        // DOUBLE SIDED
        // ============================================

        Cull Off

        Blend SrcAlpha One
        ZWrite Off

        Pass
        {
            Name "Hologram"

            Tags
            {
                "LightMode" = "UniversalForward"
            }

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 viewDirWS : TEXCOORD2;
                float2 uv : TEXCOORD3;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)

                float4 _HologramColor;

                float _ImageIntensity;

                float _GlowStrength;
                float _Transparency;

                float _GradientBottom;
                float _GradientTop;
                float _GradientPower;

                float _FresnelPower;
                float _FresnelStrength;

                float _ScanlineIntensity;
                float _ScanlineScale;
                float _ScanlineSpeed;

                float _FlickerStrength;
                float _FlickerSpeed;

                float _DistortionStrength;
                float _DistortionSpeed;

            CBUFFER_END


            // ============================================
            // NOISE
            // ============================================

            float hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);

                return frac(p.x * p.y);
            }


            float noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);

                f = f * f * (3.0 - 2.0 * f);

                float a = hash(i);
                float b = hash(i + float2(1, 0));
                float c = hash(i + float2(0, 1));
                float d = hash(i + float2(1, 1));

                return lerp(
                    lerp(a, b, f.x),
                    lerp(c, d, f.x),
                    f.y
                );
            }


            // ============================================
            // VERTEX
            // ============================================

            Varyings vert(Attributes input)
            {
                Varyings output;

                VertexPositionInputs vertexInput =
                    GetVertexPositionInputs(
                        input.positionOS.xyz
                    );

                VertexNormalInputs normalInput =
                    GetVertexNormalInputs(
                        input.normalOS
                    );

                output.positionHCS =
                    vertexInput.positionCS;

                output.positionWS =
                    vertexInput.positionWS;

                output.normalWS =
                    normalize(normalInput.normalWS);

                output.viewDirWS =
                    normalize(
                        GetCameraPositionWS()
                        - vertexInput.positionWS
                    );

                output.uv = input.uv;

                return output;
            }


            // ============================================
            // FRAGMENT
            // ============================================

            half4 frag(
                Varyings input,
                FRONT_FACE_TYPE facing : FRONT_FACE_SEMANTIC
            ) : SV_Target
            {
                float time = _Time.y;


                // ========================================
                // DOUBLE-SIDED NORMAL
                // ========================================

                float3 normalWS =
                    normalize(input.normalWS);

                // Flip normal for back faces
                #if defined(SHADER_API_D3D11) || \
                    defined(SHADER_API_D3D12) || \
                    defined(SHADER_API_VULKAN) || \
                    defined(SHADER_API_METAL)

                    if (!facing)
                    {
                        normalWS = -normalWS;
                    }

                #endif


                // ========================================
                // IMAGE
                // ========================================

                float4 image =
                    SAMPLE_TEXTURE2D(
                        _MainTex,
                        sampler_MainTex,
                        input.uv
                    );

                float3 imageColor =
                    image.rgb * _ImageIntensity;

                float imageAlpha =
                    image.a;


                // ========================================
                // BOTTOM → TOP GRADIENT
                // ========================================

                float gradient =
                    smoothstep(
                        _GradientBottom,
                        _GradientTop,
                        input.uv.y
                    );

                gradient =
                    pow(
                        saturate(gradient),
                        _GradientPower
                    );


                // ========================================
                // FRESNEL
                // ========================================

                float viewDot =
                    saturate(
                        dot(
                            normalWS,
                            normalize(input.viewDirWS)
                        )
                    );

                float fresnel =
                    1.0 - viewDot;

                fresnel =
                    pow(
                        fresnel,
                        _FresnelPower
                    );

                float edgeGlow =
                    fresnel * _FresnelStrength;


                // ========================================
                // SCANLINES
                // ========================================

                float scanPosition =
                    input.positionWS.y
                    * _ScanlineScale
                    + time
                    * _ScanlineSpeed;

                float scanline =
                    sin(scanPosition);

                scanline =
                    scanline * 0.5 + 0.5;

                scanline =
                    lerp(
                        1.0 - _ScanlineIntensity,
                        1.0,
                        scanline
                    );


                // ========================================
                // DIGITAL NOISE
                // ========================================

                float2 noiseUV =
                    input.uv * 8.0
                    + time * _DistortionSpeed;

                float noiseValue =
                    noise(noiseUV);

                float distortion =
                    lerp(
                        1.0 - _DistortionStrength,
                        1.0 + _DistortionStrength,
                        noiseValue
                    );


                // ========================================
                // FLICKER
                // ========================================

                float flickerNoise =
                    noise(
                        float2(
                            time * _FlickerSpeed,
                            0.0
                        )
                    );

                float flicker =
                    lerp(
                        1.0 - _FlickerStrength,
                        1.0,
                        flickerNoise
                    );


                // ========================================
                // COLOR
                // ========================================

                float3 finalImage =
                    imageColor *
                    _HologramColor.rgb;

                float3 baseColor =
                    _HologramColor.rgb +
                    finalImage;


                // ========================================
                // INTENSITY
                // ========================================

                float intensity =
                    _GlowStrength
                    * scanline
                    * distortion
                    * flicker;

                intensity += edgeGlow;

                intensity *= gradient;


                float3 finalColor =
                    baseColor * intensity;


                // ========================================
                // ALPHA
                // ========================================

                float alpha =
                    _Transparency
                    * gradient
                    * flicker;

                alpha *=
                    max(imageAlpha, 0.1);

                alpha +=
                    fresnel *
                    _FresnelStrength *
                    0.15;

                alpha =
                    saturate(alpha);


                return half4(
                    finalColor,
                    alpha
                );
            }

            ENDHLSL
        }
    }

    FallBack Off
}