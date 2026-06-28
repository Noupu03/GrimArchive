Shader "Custom/FX/URP_FlatFX_Lab_v03"
{
    Properties
    {
        [Header(Render Settings)]
        _SrcBlend ("Src Blend 5=SrcAlpha", Float) = 5
        _DstBlend ("Dst Blend 10=Alpha / 1=Add", Float) = 10
        _Cull ("Cull 0=Off", Float) = 0
        _ZWrite ("Z Write 0=Off", Float) = 0

        [Header(Main)]
        _MainTex ("Main Texture", 2D) = "white" {}
        [HDR] _TintColor ("Tint Color", Color) = (1,1,1,1)
        _Alpha ("Global Alpha", Range(0,1)) = 1
        _UseVertexColor ("Use Vertex Color", Range(0,1)) = 1

        [Header(Emission)]
        [HDR] _EmissionColor ("Emission Color", Color) = (1,1,1,1)
        _EmissionPower ("Emission Power", Range(0,30)) = 1

        [Header(UV Scroll)]
        _MainScrollX ("Main Scroll X", Float) = 0
        _MainScrollY ("Main Scroll Y", Float) = 0

        [Header(Noise)]
        _NoiseScale ("Noise Scale", Range(0.1,100)) = 12
        _NoiseStrength ("Noise Strength", Range(0,1)) = 0
        _NoiseContrast ("Noise Contrast", Range(0.1,8)) = 1
        _NoiseScrollX ("Noise Scroll X", Float) = 0
        _NoiseScrollY ("Noise Scroll Y", Float) = 0

        [Header(Noise Texture)]
        _NoiseTex ("Noise Texture", 2D) = "white" {}
        _UseNoiseTex ("Use Noise Texture", Range(0,1)) = 0
        _NoiseTexStrength ("Noise Texture Strength", Range(0,1)) = 0

        [Header(Mask)]
        _MaskTex ("Mask Texture", 2D) = "white" {}
        _MaskStrength ("Mask Strength", Range(0,1)) = 0
        _InvertMask ("Invert Mask", Range(0,1)) = 0

        [Header(Dissolve)]
        _DissolveAmount ("Dissolve Amount", Range(0,1)) = 0
        _DissolveSoftness ("Dissolve Softness", Range(0.001,1)) = 0.15
        [HDR] _DissolveEdgeColor ("Dissolve Edge Color", Color) = (1,0.5,0,1)
        _DissolveEdgePower ("Dissolve Edge Power", Range(0,30)) = 0
        _DissolveEdgeWidth ("Dissolve Edge Width", Range(0.001,0.5)) = 0.08

        [Header(Gradient)]
        _GradientStrength ("Gradient Strength", Range(0,1)) = 0
        _GradientSource ("Gradient Source 0=UVY 1=UVX 2=Noise 3=Alpha", Range(0,3)) = 0
        [HDR] _GradientColorA ("Gradient Color A", Color) = (1,0,0,1)
        [HDR] _GradientColorB ("Gradient Color B", Color) = (1,1,0,1)
        [HDR] _GradientColorC ("Gradient Color C", Color) = (1,1,1,1)
        _GradientOffset ("Gradient Offset", Range(-1,1)) = 0
        _GradientScale ("Gradient Scale", Range(0.01,4)) = 1

        [Header(Distortion)]
        _DistortionStrength ("UV Distortion Strength", Range(0,0.3)) = 0
        _DistortionScale ("UV Distortion Scale", Range(0.1,100)) = 16
        _DistortionScrollX ("UV Distortion Scroll X", Float) = 0
        _DistortionScrollY ("UV Distortion Scroll Y", Float) = 0

        [Header(Flicker)]
        _FlickerAmount ("Flicker Amount", Range(0,1)) = 0
        _FlickerSpeed ("Flicker Speed", Range(0,80)) = 8

        [Header(Pixel UV)]
        _PixelAmount ("Pixel Amount 0=Off", Range(0,512)) = 0

        [Header(Texture Outline)]
        _OutlineWidth ("Outline Width 0=Off", Range(0,8)) = 0
        [HDR] _OutlineColor ("Outline Color", Color) = (1,1,1,1)
        _OutlinePower ("Outline Power", Range(0,20)) = 1

        [Header(Soft Edge For Line Trail)]
        _SoftEdge ("Soft Edge", Range(0,1)) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
        }

        Blend [_SrcBlend] [_DstBlend]
        Cull [_Cull]
        ZWrite [_ZWrite]
        ZTest LEqual

        Pass
        {
            Name "FlatFXUnlit"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            TEXTURE2D(_NoiseTex);
            SAMPLER(sampler_NoiseTex);

            TEXTURE2D(_MaskTex);
            SAMPLER(sampler_MaskTex);

            float4 _MainTex_TexelSize;

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _NoiseTex_ST;
                float4 _MaskTex_ST;

                float4 _TintColor;
                float _Alpha;
                float _UseVertexColor;

                float4 _EmissionColor;
                float _EmissionPower;

                float _MainScrollX;
                float _MainScrollY;

                float _NoiseScale;
                float _NoiseStrength;
                float _NoiseContrast;
                float _NoiseScrollX;
                float _NoiseScrollY;

                float _UseNoiseTex;
                float _NoiseTexStrength;

                float _MaskStrength;
                float _InvertMask;

                float _DissolveAmount;
                float _DissolveSoftness;
                float4 _DissolveEdgeColor;
                float _DissolveEdgePower;
                float _DissolveEdgeWidth;

                float _GradientStrength;
                float _GradientSource;
                float4 _GradientColorA;
                float4 _GradientColorB;
                float4 _GradientColorC;
                float _GradientOffset;
                float _GradientScale;

                float _DistortionStrength;
                float _DistortionScale;
                float _DistortionScrollX;
                float _DistortionScrollY;

                float _FlickerAmount;
                float _FlickerSpeed;

                float _PixelAmount;

                float _OutlineWidth;
                float4 _OutlineColor;
                float _OutlinePower;

                float _SoftEdge;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float valueNoise(float2 uv)
            {
                float2 i = floor(uv);
                float2 f = frac(uv);

                float a = hash21(i);
                float b = hash21(i + float2(1.0, 0.0));
                float c = hash21(i + float2(0.0, 1.0));
                float d = hash21(i + float2(1.0, 1.0));

                float2 u = f * f * (3.0 - 2.0 * f);

                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float ApplyContrast(float v, float contrast)
            {
                return saturate((v - 0.5) * contrast + 0.5);
            }

            float3 ThreeColorGradient(float t)
            {
                t = saturate(t * _GradientScale + _GradientOffset);

                float3 ab = lerp(_GradientColorA.rgb, _GradientColorB.rgb, saturate(t * 2.0));
                float3 bc = lerp(_GradientColorB.rgb, _GradientColorC.rgb, saturate((t - 0.5) * 2.0));

                return lerp(ab, bc, step(0.5, t));
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color = IN.color;

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float time = _Time.y;

                float2 baseUV = IN.uv;
                float2 uv = baseUV;

                if (_PixelAmount > 1.0)
                {
                    uv = floor(uv * _PixelAmount) / _PixelAmount;
                }

                uv += float2(_MainScrollX, _MainScrollY) * time;

                float2 procNoiseUV = baseUV * _NoiseScale;
                procNoiseUV += float2(_NoiseScrollX, _NoiseScrollY) * time;

                float procNoise = valueNoise(procNoiseUV);
                procNoise = ApplyContrast(procNoise, _NoiseContrast);

                float2 noiseTexUV = TRANSFORM_TEX(baseUV, _NoiseTex);
                float noiseTex = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, noiseTexUV).r;

                float noise = lerp(procNoise, noiseTex, saturate(_UseNoiseTex * _NoiseTexStrength));

                float2 distortionUV = baseUV * _DistortionScale;
                distortionUV += float2(_DistortionScrollX, _DistortionScrollY) * time;

                float d1 = valueNoise(distortionUV);
                float d2 = valueNoise(distortionUV + float2(17.13, 91.77));
                float2 distortion = float2(d1 - 0.5, d2 - 0.5);

                if (_DistortionStrength > 0.0001)
                {
                    uv += distortion * _DistortionStrength;
                }

                half4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);

                half4 vertexColor = lerp(half4(1,1,1,1), IN.color, _UseVertexColor);
                half4 col = tex * _TintColor * vertexColor;

                if (_NoiseStrength > 0.0001)
                {
                    float noiseMix = lerp(1.0, noise, _NoiseStrength);
                    col.rgb *= noiseMix;
                    col.a *= lerp(1.0, noise, _NoiseStrength * 0.5);
                }

                if (_MaskStrength > 0.0001)
                {
                    float2 maskUV = TRANSFORM_TEX(baseUV, _MaskTex);
                    float maskValue = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, maskUV).r;
                    maskValue = lerp(maskValue, 1.0 - maskValue, _InvertMask);
                    col.a *= lerp(1.0, maskValue, _MaskStrength);
                }

                if (_FlickerAmount > 0.0001)
                {
                    float flicker = sin(time * _FlickerSpeed + noise * 6.2831853) * 0.5 + 0.5;
                    flicker = lerp(1.0, flicker, _FlickerAmount);
                    col.rgb *= flicker;
                }

                if (_DissolveAmount > 0.0001)
                {
                    float dissolveMask = smoothstep(
                        _DissolveAmount - _DissolveSoftness,
                        _DissolveAmount + _DissolveSoftness,
                        noise
                    );

                    float edgeA = smoothstep(
                        _DissolveAmount,
                        _DissolveAmount + _DissolveEdgeWidth,
                        noise
                    );

                    float edgeB = smoothstep(
                        _DissolveAmount + _DissolveEdgeWidth,
                        _DissolveAmount + _DissolveEdgeWidth * 2.0,
                        noise
                    );

                    float edgeMask = saturate(edgeA - edgeB);

                    col.a *= dissolveMask;
                    col.rgb += _DissolveEdgeColor.rgb * edgeMask * _DissolveEdgePower;
                }

                if (_GradientStrength > 0.0001)
                {
                    float gradientT = baseUV.y;

                    if (_GradientSource >= 0.5 && _GradientSource < 1.5)
                    {
                        gradientT = baseUV.x;
                    }
                    else if (_GradientSource >= 1.5 && _GradientSource < 2.5)
                    {
                        gradientT = noise;
                    }
                    else if (_GradientSource >= 2.5)
                    {
                        gradientT = col.a;
                    }

                    float3 gradientColor = ThreeColorGradient(gradientT);
                    col.rgb = lerp(col.rgb, col.rgb * gradientColor, _GradientStrength);
                }

                if (_OutlineWidth > 0.0001)
                {
                    float2 texel = _MainTex_TexelSize.xy * _OutlineWidth;

                    float a0 = tex.a;
                    float a1 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(texel.x, 0)).a;
                    float a2 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv - float2(texel.x, 0)).a;
                    float a3 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv + float2(0, texel.y)).a;
                    float a4 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv - float2(0, texel.y)).a;

                    float outlineMask = saturate(max(max(a1, a2), max(a3, a4)) - a0);
                    col.rgb += _OutlineColor.rgb * outlineMask * _OutlinePower;
                    col.a = max(col.a, outlineMask * _OutlineColor.a);
                }

                if (_SoftEdge > 0.0001)
                {
                    float centerMask = 1.0 - abs(baseUV.y * 2.0 - 1.0);
                    float soft = smoothstep(0.0, _SoftEdge, centerMask);
                    col.a *= soft;
                }

                col.a *= _Alpha;
                col.rgb *= _EmissionColor.rgb * _EmissionPower;

                return col;
            }

            ENDHLSL
        }
    }

    FallBack Off
    CustomEditor "FXFlatLabGUI"
}