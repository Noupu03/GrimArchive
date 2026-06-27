using UnityEditor;
using UnityEngine;

public class FXAllInOneLabGUI : ShaderGUI
{
    static bool showRender = true;
    static bool showMain = true;
    static bool showUV = true;
    static bool showNoise = true;
    static bool showMask = true;
    static bool showDissolve = true;
    static bool showGradient = true;
    static bool showFresnel = true;
    static bool showDistortion = true;
    static bool showExtra = true;
    static bool showDepth = true;
    static bool showVertex = false;

    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] props)
    {
        EditorGUILayout.Space(4);

        EditorGUILayout.HelpBox(
            "FX All-in-One Lab v0.2\n" +
            "실험용 셰이더입니다. 기능을 많이 넣어 머티리얼에서 테스트한 뒤, 실제로 쓰는 기능만 남긴 경량 셰이더로 분리하는 용도입니다.",
            MessageType.Warning
        );

        DrawPresetButtons(materialEditor);

        DrawRenderSettings(materialEditor, props);
        DrawMain(materialEditor, props);
        DrawUV(materialEditor, props);
        DrawNoise(materialEditor, props);
        DrawMask(materialEditor, props);
        DrawDissolve(materialEditor, props);
        DrawGradient(materialEditor, props);
        DrawFresnel(materialEditor, props);
        DrawDistortion(materialEditor, props);
        DrawExtra(materialEditor, props);
        DrawDepth(materialEditor, props);
        DrawVertex(materialEditor, props);

        EditorGUILayout.Space(6);

        EditorGUILayout.HelpBox(
            "권장 흐름: 이 셰이더로 머티리얼 값을 실험 → 실제 사용한 기능 목록 정리 → AI에게 미사용 기능 제거 요청 → 용도별 경량 셰이더 생성.",
            MessageType.Info
        );
    }

    static MaterialProperty P(MaterialProperty[] props, string name)
    {
        return FindProperty(name, props, false);
    }

    static void DrawProp(MaterialEditor editor, MaterialProperty[] props, string name)
    {
        MaterialProperty p = P(props, name);
        if (p != null)
        {
            editor.ShaderProperty(p, p.displayName);
        }
    }

    static void DrawTexture(MaterialEditor editor, MaterialProperty[] props, string texName, string colorName = null)
    {
        MaterialProperty tex = P(props, texName);
        MaterialProperty color = colorName != null ? P(props, colorName) : null;

        if (tex != null)
        {
            editor.TexturePropertySingleLine(new GUIContent(tex.displayName), tex, color);
        }
    }

    static void DrawPresetButtons(MaterialEditor editor)
    {
        EditorGUILayout.LabelField("Quick Blend Presets", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Alpha"))
        {
            SetFloat(editor, "_SrcBlend", 5f);   // SrcAlpha
            SetFloat(editor, "_DstBlend", 10f);  // OneMinusSrcAlpha
            SetFloat(editor, "_ZWrite", 0f);
            SetFloat(editor, "_Cull", 0f);
        }

        if (GUILayout.Button("Additive Glow"))
        {
            SetFloat(editor, "_SrcBlend", 5f);   // SrcAlpha
            SetFloat(editor, "_DstBlend", 1f);   // One
            SetFloat(editor, "_ZWrite", 0f);
            SetFloat(editor, "_Cull", 0f);
            SetFloat(editor, "_EmissionPower", 4f);
        }

        if (GUILayout.Button("Line / Trail"))
        {
            SetFloat(editor, "_SrcBlend", 5f);
            SetFloat(editor, "_DstBlend", 1f);
            SetFloat(editor, "_ZWrite", 0f);
            SetFloat(editor, "_Cull", 0f);
            SetFloat(editor, "_SoftEdge", 0.25f);
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.HelpBox(
            "Alpha: 일반 투명 / Additive Glow: 빛나는 파티클 / Line-Trail: 라인렌더러와 트레일렌더러 테스트용.",
            MessageType.Info
        );
    }

    static void SetFloat(MaterialEditor editor, string propertyName, float value)
    {
        editor.RegisterPropertyChangeUndo("FX Shader Preset");

        foreach (Object target in editor.targets)
        {
            Material mat = target as Material;
            if (mat != null && mat.HasProperty(propertyName))
            {
                mat.SetFloat(propertyName, value);
                EditorUtility.SetDirty(mat);
            }
        }
    }

    static void DrawRenderSettings(MaterialEditor editor, MaterialProperty[] props)
    {
        showRender = EditorGUILayout.BeginFoldoutHeaderGroup(showRender, "Render Settings");
        if (showRender)
        {
            EditorGUILayout.HelpBox(
                "렌더링 방식입니다. 파티클/라인/트레일은 보통 ZWrite Off, Cull Off를 씁니다.\n\n" +
                "SrcBlend 5 = SrcAlpha\n" +
                "DstBlend 10 = 일반 투명\n" +
                "DstBlend 1 = Additive 발광",
                MessageType.Info
            );

            DrawProp(editor, props, "_SrcBlend");
            DrawProp(editor, props, "_DstBlend");
            DrawProp(editor, props, "_Cull");
            DrawProp(editor, props, "_ZWrite");
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    static void DrawMain(MaterialEditor editor, MaterialProperty[] props)
    {
        showMain = EditorGUILayout.BeginFoldoutHeaderGroup(showMain, "Main / Emission");
        if (showMain)
        {
            DrawTexture(editor, props, "_MainTex", "_TintColor");
            DrawProp(editor, props, "_Alpha");
            DrawProp(editor, props, "_UseVertexColor");

            EditorGUILayout.HelpBox(
                "Use Vertex Color는 파티클 시스템의 Color over Lifetime / Alpha over Lifetime을 반영하기 위한 값입니다. 파티클용이면 보통 1로 둡니다.",
                MessageType.Info
            );

            DrawProp(editor, props, "_EmissionColor");
            DrawProp(editor, props, "_EmissionPower");

            EditorGUILayout.HelpBox(
                "Emission Power가 높을수록 밝아집니다. Bloom을 켜면 발광 느낌이 더 강해집니다. 너무 높이면 색이 날아갈 수 있습니다.",
                MessageType.Warning
            );
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    static void DrawUV(MaterialEditor editor, MaterialProperty[] props)
    {
        showUV = EditorGUILayout.BeginFoldoutHeaderGroup(showUV, "UV Scroll / Flipbook");
        if (showUV)
        {
            EditorGUILayout.HelpBox(
                "UV Scroll은 텍스처가 흐르는 느낌을 만듭니다. 불, 마법, 에너지 라인, 레이저에 자주 씁니다.",
                MessageType.Info
            );

            DrawProp(editor, props, "_MainScrollX");
            DrawProp(editor, props, "_MainScrollY");

            EditorGUILayout.Space(4);
            DrawProp(editor, props, "_UseFlipbook");
            DrawProp(editor, props, "_FlipbookColumns");
            DrawProp(editor, props, "_FlipbookRows");
            DrawProp(editor, props, "_FlipbookFrame");
            DrawProp(editor, props, "_FlipbookSpeed");

            EditorGUILayout.HelpBox(
                "Flipbook은 한 장의 텍스처 안에 여러 프레임이 들어간 경우 사용합니다. Unity Particle System의 Texture Sheet Animation과 역할이 겹칠 수 있으므로 둘 중 하나만 쓰는 것을 권장합니다.",
                MessageType.Warning
            );
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    static void DrawNoise(MaterialEditor editor, MaterialProperty[] props)
    {
        showNoise = EditorGUILayout.BeginFoldoutHeaderGroup(showNoise, "Noise");
        if (showNoise)
        {
            EditorGUILayout.HelpBox(
                "Noise는 불규칙함을 만드는 핵심 모듈입니다. 불, 연기, 마법, 디졸브, 전기 느낌 대부분에 사용됩니다.",
                MessageType.Info
            );

            DrawProp(editor, props, "_NoiseScale");
            DrawProp(editor, props, "_NoiseStrength");
            DrawProp(editor, props, "_NoiseContrast");
            DrawProp(editor, props, "_NoiseScrollX");
            DrawProp(editor, props, "_NoiseScrollY");

            EditorGUILayout.HelpBox(
                "Noise Strength를 올리면 색과 알파가 불규칙해집니다. 너무 높이면 텍스처가 지저분해질 수 있습니다.",
                MessageType.Warning
            );

            DrawTexture(editor, props, "_NoiseTex");
            DrawProp(editor, props, "_UseNoiseTex");
            DrawProp(editor, props, "_NoiseTexStrength");
            DrawProp(editor, props, "_NoiseTexScrollX");
            DrawProp(editor, props, "_NoiseTexScrollY");

            EditorGUILayout.HelpBox(
                "Noise Texture를 쓰면 절차적 노이즈보다 원하는 패턴을 더 정확히 만들 수 있습니다. 대신 텍스처 샘플링 비용이 늘어납니다.",
                MessageType.Info
            );
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    static void DrawMask(MaterialEditor editor, MaterialProperty[] props)
    {
        showMask = EditorGUILayout.BeginFoldoutHeaderGroup(showMask, "Mask");
        if (showMask)
        {
            DrawTexture(editor, props, "_MaskTex");
            DrawProp(editor, props, "_MaskStrength");
            DrawProp(editor, props, "_InvertMask");

            EditorGUILayout.HelpBox(
                "Mask는 특정 영역만 보이게 하거나 사라지게 하는 용도입니다. 흰색은 보임, 검은색은 숨김으로 생각하면 됩니다.",
                MessageType.Info
            );
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    static void DrawDissolve(MaterialEditor editor, MaterialProperty[] props)
    {
        showDissolve = EditorGUILayout.BeginFoldoutHeaderGroup(showDissolve, "Dissolve");
        if (showDissolve)
        {
            DrawProp(editor, props, "_DissolveAmount");
            DrawProp(editor, props, "_DissolveSoftness");
            DrawProp(editor, props, "_DissolveEdgeColor");
            DrawProp(editor, props, "_DissolveEdgePower");
            DrawProp(editor, props, "_DissolveEdgeWidth");

            EditorGUILayout.HelpBox(
                "Dissolve Amount를 올리면 노이즈 기준으로 사라집니다. Edge Power는 사라지는 경계에 빛을 더합니다.",
                MessageType.Info
            );
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    static void DrawGradient(MaterialEditor editor, MaterialProperty[] props)
    {
        showGradient = EditorGUILayout.BeginFoldoutHeaderGroup(showGradient, "Gradient");
        if (showGradient)
        {
            DrawProp(editor, props, "_GradientStrength");
            DrawProp(editor, props, "_GradientSource");
            DrawProp(editor, props, "_GradientColorA");
            DrawProp(editor, props, "_GradientColorB");
            DrawProp(editor, props, "_GradientColorC");
            DrawProp(editor, props, "_GradientOffset");
            DrawProp(editor, props, "_GradientScale");

            EditorGUILayout.HelpBox(
                "Gradient는 색 변화 모듈입니다. 불꽃, 마법, 레이저처럼 한 색이 아니라 여러 색이 섞이는 이펙트에 사용합니다.\n\n" +
                "Source 0=UVY, 1=UVX, 2=Noise, 3=Alpha",
                MessageType.Info
            );
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    static void DrawFresnel(MaterialEditor editor, MaterialProperty[] props)
    {
        showFresnel = EditorGUILayout.BeginFoldoutHeaderGroup(showFresnel, "Fresnel / Rim");
        if (showFresnel)
        {
            DrawProp(editor, props, "_FresnelStrength");
            DrawProp(editor, props, "_FresnelColor");
            DrawProp(editor, props, "_FresnelPower");

            EditorGUILayout.HelpBox(
                "Fresnel은 카메라 기준 가장자리를 밝게 만드는 효과입니다. 구체형 보호막, 오라, 홀로그램에는 좋지만, 평평한 파티클에서는 효과가 약하거나 이상해 보일 수 있습니다.",
                MessageType.Warning
            );
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    static void DrawDistortion(MaterialEditor editor, MaterialProperty[] props)
    {
        showDistortion = EditorGUILayout.BeginFoldoutHeaderGroup(showDistortion, "Distortion / Refraction");
        if (showDistortion)
        {
            DrawProp(editor, props, "_DistortionStrength");
            DrawProp(editor, props, "_DistortionScale");
            DrawProp(editor, props, "_DistortionScrollX");
            DrawProp(editor, props, "_DistortionScrollY");

            EditorGUILayout.HelpBox(
                "UV Distortion은 자신의 텍스처 좌표를 흔드는 효과입니다. 불꽃, 연기, 마법 일렁임에 사용합니다.",
                MessageType.Info
            );

            DrawProp(editor, props, "_ScreenDistortionStrength");
            DrawProp(editor, props, "_ScreenDistortionBlend");

            EditorGUILayout.HelpBox(
                "Screen Refraction은 화면 뒤 배경을 왜곡하는 효과입니다. URP Asset에서 Opaque Texture가 켜져 있어야 제대로 보입니다. 모바일/저사양에서는 비용이 커질 수 있습니다.",
                MessageType.Warning
            );
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    static void DrawExtra(MaterialEditor editor, MaterialProperty[] props)
    {
        showExtra = EditorGUILayout.BeginFoldoutHeaderGroup(showExtra, "Extra: Flicker / Pixel / Outline / Soft Edge");
        if (showExtra)
        {
            DrawProp(editor, props, "_FlickerAmount");
            DrawProp(editor, props, "_FlickerSpeed");

            EditorGUILayout.HelpBox(
                "Flicker는 시간 기반 깜빡임입니다. 전기, 불안정한 마법, 고장난 에너지에 좋습니다.",
                MessageType.Info
            );

            DrawProp(editor, props, "_PixelAmount");

            EditorGUILayout.HelpBox(
                "Pixel Amount는 UV 기준 픽셀화입니다. 화면 전체 픽셀화가 아니라 텍스처 좌표를 계단식으로 끊는 효과입니다.",
                MessageType.Warning
            );

            DrawProp(editor, props, "_OutlineWidth");
            DrawProp(editor, props, "_OutlineColor");
            DrawProp(editor, props, "_OutlinePower");

            EditorGUILayout.HelpBox(
                "Outline은 텍스처 알파를 주변 샘플링해서 만드는 간단한 외곽선입니다. 샘플링 횟수가 늘어나므로 많이 쓰면 비용이 증가합니다.",
                MessageType.Warning
            );

            DrawProp(editor, props, "_SoftEdge");

            EditorGUILayout.HelpBox(
                "Soft Edge는 Line Renderer / Trail Renderer에서 UV.y 기준으로 가장자리를 부드럽게 만드는 용도입니다. 일반 파티클에서는 결과가 애매할 수 있습니다.",
                MessageType.Info
            );
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    static void DrawDepth(MaterialEditor editor, MaterialProperty[] props)
    {
        showDepth = EditorGUILayout.BeginFoldoutHeaderGroup(showDepth, "Depth / Camera Fade");
        if (showDepth)
        {
            DrawProp(editor, props, "_SoftParticleDistance");

            EditorGUILayout.HelpBox(
                "Soft Particle은 바닥/벽과 만나는 경계를 부드럽게 사라지게 합니다. URP Asset 또는 Camera에서 Depth Texture가 켜져 있어야 합니다.",
                MessageType.Warning
            );

            DrawProp(editor, props, "_CameraFadeStrength");
            DrawProp(editor, props, "_CameraFadeNear");
            DrawProp(editor, props, "_CameraFadeFar");

            EditorGUILayout.HelpBox(
                "Camera Fade는 카메라에 너무 가까운 이펙트를 서서히 사라지게 합니다. 화면을 가리는 큰 파티클에 유용합니다.",
                MessageType.Info
            );
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    static void DrawVertex(MaterialEditor editor, MaterialProperty[] props)
    {
        showVertex = EditorGUILayout.BeginFoldoutHeaderGroup(showVertex, "Vertex Offset Experimental");
        if (showVertex)
        {
            DrawProp(editor, props, "_VertexOffsetStrength");
            DrawProp(editor, props, "_VertexOffsetScale");
            DrawProp(editor, props, "_VertexOffsetSpeed");

            EditorGUILayout.HelpBox(
                "Vertex Offset은 메시 정점을 움직입니다. 파티클 쿼드나 라인/트레일에서는 기대한 것보다 효과가 약할 수 있습니다. 메시 이펙트에서 더 잘 보입니다.",
                MessageType.Warning
            );
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }
}