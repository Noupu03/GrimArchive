Shader "Custom/FX/URP_AllInOne_Lab_v02"
{
    Properties
    {
        [Header(Render Settings)]
        _SrcBlend ("Src Blend 5=SrcAlpha", Float) = 5
        _DstBlend ("Dst Blend 10=Alpha / 1=Add", Float) = 10
        _Cull ("Cull 0=Off / 2=Back", Float) = 0
        _ZWrite ("Z Write 0=Off / 1=On", Float) = 0

        [Header(Main)]
        _MainTex ("Main Texture", 2D) = "white" {}
        [HDR] _TintColor ("Tint Color", Color) = (1,1,1,1)
        _Alpha ("Global Alpha", Range(0,1)) = 1
        _UseVertexColor ("Use Vertex Color 0/1", Range(0,1)) = 1

        [Header(Emission Glow)]
        [HDR] _EmissionColor ("Emission Color", Color) = (1,1,1,1)
        _EmissionPower ("Emission Power", Range(0,30)) = 1

        [Header(Main UV Scroll)]
        _MainScrollX ("Main Scroll X", Float) = 0
        _MainScrollY ("Main Scroll Y", Float) = 0

        [Header(Flipbook Manual)]
        _UseFlipbook ("Use Flipbook 0/1", Range(0,1)) = 0
        _FlipbookColumns ("Flipbook Columns", Range(1,16)) = 1
        _FlipbookRows ("Flipbook Rows", Range(1,16)) = 1
        _FlipbookFrame ("Flipbook Frame", Float) = 0
        _FlipbookSpeed ("Flipbook Speed", Float) = 0

        [Header(Procedural Noise)]
        _NoiseScale ("Noise Scale", Range(0.1,100)) = 12
        _NoiseStrength ("Noise Strength", Range(0,1)) = 0
        _NoiseContrast ("Noise Contrast", Range(0.1,8)) = 1
        _NoiseScrollX ("Noise Scroll X", Float) = 0
        _NoiseScrollY ("Noise Scroll Y", Float) = 0

        [Header(Noise Texture Optional)]
        _NoiseTex ("Noise Texture", 2D) = "white" {}
        _UseNoiseTex ("Use Noise Texture 0/1", Range(0,1)) = 0
        _NoiseTexStrength ("Noise Texture Strength", Range(0,1)) = 0
        _NoiseTexScrollX ("Noise Texture Scroll X", Float) = 0
        _NoiseTexScrollY ("Noise Texture Scroll Y", Float) = 0

        [Header(Mask)]
        _MaskTex ("Mask Texture", 2D) = "white" {}
        _MaskStrength ("Mask Strength", Range(0,1)) = 0
        _InvertMask ("Invert Mask 0/1", Range(0,1)) = 0

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

        [Header(Fresnel Rim)]
        _FresnelStrength ("Fresnel Strength", Range(0,1)) = 0
        [HDR] _FresnelColor ("Fresnel Color", Color) = (0.5,0.8,1,1)
        _FresnelPower ("Fresnel Power", Range(0.1,10)) = 3

        [Header(Distortion UV)]
        _DistortionStrength ("UV Distortion Strength", Range(0,0.3)) = 0
        _DistortionScale ("UV Distortion Scale", Range(0.1,100)) = 16
        _DistortionScrollX ("UV Distortion Scroll X", Float) = 0
        _DistortionScrollY ("UV Distortion Scroll Y", Float) = 0

        [Header(Screen Refraction Requires Opaque Texture)]
        _ScreenDistortionStrength ("Screen Distortion Strength", Range(0,0.05)) = 0
        _ScreenDistortionBlend ("Screen Distortion Blend", Range(0,1)) = 0

        [Header(Flicker)]
        _FlickerAmount ("Flicker Amount", Range(0,1)) = 0
        _FlickerSpeed ("Flicker Speed", Range(0,80)) = 8

        [Header(Pixel UV)]
        _PixelAmount ("Pixel Amount 0=Off", Range(0,512)) = 0

        [Header(Texture Outline)]
        _OutlineWidth ("Outline Width 0=Off", Range(0,8)) = 0
        [HDR] _OutlineColor ("Outline Color", Color) = (1,1,1,1)
        _OutlinePower ("Outline Power", Range(0,20)) = 1

        [Header(Soft Edge for Line Trail)]
        _SoftEdge ("Soft Edge", Range(0,1)) = 0

        [Header(Soft Particle Requires Depth Texture)]
        _SoftParticleDistance ("Soft Particle Distance 0=Off", Range(0,10)) = 0

        [Header(Camera Fade)]
        _CameraFadeStrength ("Camera Fade Strength", Range(0,1)) = 0
        _CameraFadeNear ("Camera Fade Near", Float) = 0.2
        _CameraFadeFar ("Camera Fade Far", Float) = 1.5

        [Header(Vertex Offset Experimental)]
        _VertexOffsetStrength ("Vertex Offset Strength", Range(0,1)) = 0
        _VertexOffsetScale ("Vertex Offset Scale", Range(0.1,100)) = 10
        _VertexOffsetSpeed ("Vertex Offset Speed", Float) = 0
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
            Name "ForwardUnlit"

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

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

                float _UseFlipbook;
                float _FlipbookColumns;
                float _FlipbookRows;
                float _FlipbookFrame;
                float _FlipbookSpeed;

                float _NoiseScale;
                float _NoiseStrength;
                float _NoiseContrast;
                float _NoiseScrollX;
                float _NoiseScrollY;

                float _UseNoiseTex;
                float _NoiseTexStrength;
                float _NoiseTexScrollX;
                float _NoiseTexScrollY;

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

                float _FresnelStrength;
                float4 _FresnelColor;
                float _FresnelPower;

                float _DistortionStrength;
                float _DistortionScale;
                float _DistortionScrollX;
                float _DistortionScrollY;

                float _ScreenDistortionStrength;
                float _ScreenDistortionBlend;

                float _FlickerAmount;
                float _FlickerSpeed;

                float _PixelAmount;

                float _OutlineWidth;
                float4 _OutlineColor;
                float _OutlinePower;

                float _SoftEdge;

                float _SoftParticleDistance;

                float _CameraFadeStrength;
                float _CameraFadeNear;
                float _CameraFadeFar;

                float _VertexOffsetStrength;
                float _VertexOffsetScale;
                float _VertexOffsetSpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                float4 screenPos : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float3 normalWS : TEXCOORD3;
                float viewDepth : TEXCOORD4;
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

            float applyContrast(float v, float contrast)
            {
                return saturate((v - 0.5) * contrast + 0.5);
            }

            float2 ApplyFlipbook(float2 uv, float time)
            {
                if (_UseFlipbook < 0.5)
                {
                    return uv;
                }

                float columns = max(1.0, floor(_FlipbookColumns));
                float rows = max(1.0, floor(_FlipbookRows));
                float totalFrames = max(1.0, columns * rows);

                float frame = floor(_FlipbookFrame + time * _FlipbookSpeed);
                frame = fmod(frame, totalFrames);

                float column = fmod(frame, columns);
                float row = floor(frame / columns);

                float2 cellSize = float2(1.0 / columns, 1.0 / rows);

                uv = uv * cellSize;
                uv.x += column * cellSize.x;
                uv.y += (rows - 1.0 - row) * cellSize.y;

                return uv;
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

                float time = _Time.y;

                float3 positionOS = IN.positionOS.xyz;
                float3 normalOS = normalize(IN.normalOS);

                if (_VertexOffsetStrength > 0.0001)
                {
                    float2 offsetUV = IN.uv * _VertexOffsetScale;
                    offsetUV += time * _VertexOffsetSpeed;

                    float n = valueNoise(offsetUV);
                    positionOS += normalOS * ((n - 0.5) * _VertexOffsetStrength);
                }

                VertexPositionInputs posInput = GetVertexPositionInputs(positionOS);
                VertexNormalInputs normalInput = GetVertexNormalInputs(IN.normalOS);

                OUT.positionHCS = posInput.positionCS;
                OUT.positionWS = posInput.positionWS;
                OUT.normalWS = normalize(normalInput.normalWS);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color = IN.color;
                OUT.screenPos = ComputeScreenPos(OUT.positionHCS);

                float3 positionVS = TransformWorldToView(posInput.positionWS);
                OUT.viewDepth = -positionVS.z;

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
                procNoise = applyContrast(procNoise, _NoiseContrast);

                float2 noiseTexUV = TRANSFORM_TEX(baseUV, _NoiseTex);
                noiseTexUV += float2(_NoiseTexScrollX, _NoiseTexScrollY) * time;

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

                uv = ApplyFlipbook(uv, time);

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

                if (_FresnelStrength > 0.0001)
                {
                    float3 viewDirWS = normalize(GetWorldSpaceViewDir(IN.positionWS));
                    float fresnel = pow(1.0 - saturate(dot(normalize(IN.normalWS), viewDirWS)), _FresnelPower);
                    col.rgb += _FresnelColor.rgb * fresnel * _FresnelStrength * _FresnelColor.a;
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

                if (_SoftParticleDistance > 0.0001)
                {
                    float2 screenUV = IN.screenPos.xy / IN.screenPos.w;
                    float rawSceneDepth = SampleSceneDepth(screenUV);
                    float sceneEyeDepth = LinearEyeDepth(rawSceneDepth, _ZBufferParams);
                    float particleEyeDepth = IN.viewDepth;

                    float depthFade = saturate((sceneEyeDepth - particleEyeDepth) / _SoftParticleDistance);
                    col.a *= depthFade;
                }

                if (_CameraFadeStrength > 0.0001)
                {
                    float cameraFade = saturate((IN.viewDepth - _CameraFadeNear) / max(0.0001, _CameraFadeFar - _CameraFadeNear));
                    col.a *= lerp(1.0, cameraFade, _CameraFadeStrength);
                }

                if (_ScreenDistortionStrength > 0.0001 && _ScreenDistortionBlend > 0.0001)
                {
                    float2 screenUV = IN.screenPos.xy / IN.screenPos.w;
                    float2 screenOffset = distortion * _ScreenDistortionStrength;
                    half3 sceneColor = SampleSceneColor(screenUV + screenOffset).rgb;

                    col.rgb = lerp(col.rgb, sceneColor * col.rgb, _ScreenDistortionBlend);
                }

                col.a *= _Alpha;
                col.rgb *= _EmissionColor.rgb * _EmissionPower;

                return col;
            }

            ENDHLSL
        }
    }

    FallBack Off
    CustomEditor "FXAllInOneLabGUI"
}