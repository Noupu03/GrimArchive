using UnityEditor;
using UnityEngine;

public class FXFlatLabGUI : ShaderGUI
{
    static bool showRender = true;
    static bool showMain = true;
    static bool showMotion = true;
    static bool showNoise = true;
    static bool showMask = true;
    static bool showDissolve = true;
    static bool showGradient = true;
    static bool showDistortion = true;
    static bool showStyle = true;

    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] props)
    {
        EditorGUILayout.HelpBox(
            "Flat FX Lab v0.3\n" +
            "평면 이펙트 전용 실험 셰이더입니다.\n\n" +
            "대상: Particle System / Line Renderer / Trail Renderer / Sprite / Quad\n" +
            "목적: 머티리얼에서 수치를 조정해 이펙트 프리셋을 빠르게 생산하는 것.",
            MessageType.Info
        );

        DrawPresetButtons(materialEditor);

        DrawRender(materialEditor, props);
        DrawMain(materialEditor, props);
        DrawMotion(materialEditor, props);
        DrawNoise(materialEditor, props);
        DrawMask(materialEditor, props);
        DrawDissolve(materialEditor, props);
        DrawGradient(materialEditor, props);
        DrawDistortion(materialEditor, props);
        DrawStyle(materialEditor, props);

        EditorGUILayout.Space(8);

        EditorGUILayout.HelpBox(
            "이 셰이더에서 의도적으로 제거한 기능:\n" +
            "- Fresnel: 입체 표면 가장자리 효과라 평면 이펙트에는 부적합\n" +
            "- Vertex Offset: 평면 쿼드에서는 변형 효과가 약하거나 어색함\n" +
            "- Soft Particle: Depth Texture 의존, 현재 목표와 우선순위 낮음\n" +
            "- Screen Refraction: Opaque Texture 의존, 비용 큼\n" +
            "- Camera Fade: 3D 공간 카메라 거리 기반이라 현재 목표와 거리가 있음",
            MessageType.Warning
        );

        EditorGUILayout.HelpBox(
            "권장 작업 흐름:\n" +
            "1. 이 셰이더로 머티리얼 실험\n" +
            "2. 실제로 사용한 기능과 수치 기록\n" +
            "3. AI에게 미사용 기능 제거 요청\n" +
            "4. Particle_GlowNoise / Line_Electric / Trail_Slash 같은 경량 셰이더로 분리",
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
            editor.ShaderProperty(p, p.displayName);
    }

    static void DrawTex(MaterialEditor editor, MaterialProperty[] props, string texName, string colorName = null)
    {
        MaterialProperty tex = P(props, texName);
        MaterialProperty color = colorName != null ? P(props, colorName) : null;

        if (tex != null)
            editor.TexturePropertySingleLine(new GUIContent(tex.displayName), tex, color);
    }

    static void DrawPresetButtons(MaterialEditor editor)
    {
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Quick Presets", EditorStyles.boldLabel);

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button("Alpha"))
        {
            SetFloat(editor, "_SrcBlend", 5f);
            SetFloat(editor, "_DstBlend", 10f);
            SetFloat(editor, "_ZWrite", 0f);
            SetFloat(editor, "_Cull", 0f);
            SetFloat(editor, "_EmissionPower", 1f);
            SetFloat(editor, "_SoftEdge", 0f);
        }

        if (GUILayout.Button("Additive"))
        {
            SetFloat(editor, "_SrcBlend", 5f);
            SetFloat(editor, "_DstBlend", 1f);
            SetFloat(editor, "_ZWrite", 0f);
            SetFloat(editor, "_Cull", 0f);
            SetFloat(editor, "_EmissionPower", 4f);
            SetFloat(editor, "_SoftEdge", 0f);
        }

        if (GUILayout.Button("Line / Trail"))
        {
            SetFloat(editor, "_SrcBlend", 5f);
            SetFloat(editor, "_DstBlend", 1f);
            SetFloat(editor, "_ZWrite", 0f);
            SetFloat(editor, "_Cull", 0f);
            SetFloat(editor, "_SoftEdge", 0.25f);
            SetFloat(editor, "_EmissionPower", 5f);
        }

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.HelpBox(
            "Alpha: 일반 투명 파티클 / Additive: 빛나는 파티클 / Line-Trail: 선, 검기, 잔상 테스트용.\n\n" +
            "Blend 숫자 기본값:\n" +
            "SrcBlend 5 = SrcAlpha\n" +
            "DstBlend 10 = OneMinusSrcAlpha\n" +
            "DstBlend 1 = One",
            MessageType.Info
        );
    }

    static void SetFloat(MaterialEditor editor, string prop, float value)
    {
        editor.RegisterPropertyChangeUndo("Flat FX Preset");

        foreach (Object target in editor.targets)
        {
            Material mat = target as Material;
            if (mat != null && mat.HasProperty(prop))
            {
                mat.SetFloat(prop, value);
                EditorUtility.SetDirty(mat);
            }
        }
    }

    static void DrawRender(MaterialEditor editor, MaterialProperty[] props)
    {
        showRender = EditorGUILayout.BeginFoldoutHeaderGroup(showRender, "Render Settings");
        if (showRender)
        {
            EditorGUILayout.HelpBox(
                "렌더링 방식 설정입니다.\n" +
                "평면 이펙트는 보통 ZWrite Off, Cull Off를 씁니다.\n\n" +
                "_SrcBlend: 소스 색이 섞이는 방식\n" +
                "_DstBlend: 배경 색과 섞이는 방식\n" +
                "_Cull: 뒷면 제거 여부\n" +
                "_ZWrite: 깊이 버퍼 기록 여부",
                MessageType.Info
            );

            DrawProp(editor, props, "_SrcBlend");
            DrawProp(editor, props, "_DstBlend");
            DrawProp(editor, props, "_Cull");
            DrawProp(editor, props, "_ZWrite");

            EditorGUILayout.HelpBox(
                "추천값:\n" +
                "일반 투명: SrcBlend 5 / DstBlend 10 / Cull 0 / ZWrite 0\n" +
                "발광 효과: SrcBlend 5 / DstBlend 1 / Cull 0 / ZWrite 0\n\n" +
                "ZWrite를 켜면 투명 이펙트끼리 이상하게 가려질 수 있습니다.",
                MessageType.Warning
            );
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    static void DrawMain(MaterialEditor editor, MaterialProperty[] props)
    {
        showMain = EditorGUILayout.BeginFoldoutHeaderGroup(showMain, "Main / Emission");
        if (showMain)
        {
            EditorGUILayout.HelpBox(
                "가장 기본이 되는 색/투명도/발광 설정입니다.\n\n" +
                "_MainTex: 이펙트 기본 텍스처\n" +
                "_TintColor: 전체 색 보정\n" +
                "_Alpha: 전체 투명도\n" +
                "_UseVertexColor: 파티클 시스템 색상 반영 여부",
                MessageType.Info
            );

            DrawTex(editor, props, "_MainTex", "_TintColor");
            DrawProp(editor, props, "_Alpha");
            DrawProp(editor, props, "_UseVertexColor");

            EditorGUILayout.HelpBox(
                "Particle System의 Color over Lifetime / Alpha over Lifetime을 쓰려면 Use Vertex Color를 1로 두는 게 좋습니다.\n" +
                "Line Renderer나 Trail Renderer도 색상 그라디언트를 쓰려면 1을 권장합니다.",
                MessageType.Info
            );

            DrawProp(editor, props, "_EmissionColor");
            DrawProp(editor, props, "_EmissionPower");

            EditorGUILayout.HelpBox(
                "_EmissionColor: 발광 색\n" +
                "_EmissionPower: 발광 강도\n\n" +
                "Bloom 후처리를 켜면 Emission 효과가 훨씬 잘 보입니다.\n" +
                "Additive Blend와 함께 쓰면 마법, 불꽃, 전기 느낌을 만들기 쉽습니다.",
                MessageType.Info
            );
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    static void DrawMotion(MaterialEditor editor, MaterialProperty[] props)
    {
        showMotion = EditorGUILayout.BeginFoldoutHeaderGroup(showMotion, "UV Scroll");
        if (showMotion)
        {
            EditorGUILayout.HelpBox(
                "텍스처 좌표를 시간에 따라 이동시키는 기능입니다.\n" +
                "실제 오브젝트가 움직이는 게 아니라, 텍스처가 흐르는 것처럼 보입니다.\n\n" +
                "_MainScrollX: 가로 흐름\n" +
                "_MainScrollY: 세로 흐름",
                MessageType.Info
            );

            DrawProp(editor, props, "_MainScrollX");
            DrawProp(editor, props, "_MainScrollY");

            EditorGUILayout.HelpBox(
                "추천 용도:\n" +
                "- 불꽃: Y 방향 스크롤\n" +
                "- 레이저/전기: X 방향 스크롤\n" +
                "- 마법진/오라: 느린 X/Y 스크롤\n\n" +
                "Line/Trail에서는 UV 방향이 오브젝트 설정에 따라 다르게 보일 수 있습니다.",
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
                "Noise는 불규칙한 무늬를 만들어 색/알파/디졸브/왜곡에 활용하는 핵심 기능입니다.\n\n" +
                "_NoiseScale: 노이즈 크기\n" +
                "_NoiseStrength: 색/알파에 노이즈를 섞는 강도\n" +
                "_NoiseContrast: 노이즈 대비\n" +
                "_NoiseScrollX/Y: 노이즈 패턴 이동",
                MessageType.Info
            );

            DrawProp(editor, props, "_NoiseScale");
            DrawProp(editor, props, "_NoiseStrength");
            DrawProp(editor, props, "_NoiseContrast");
            DrawProp(editor, props, "_NoiseScrollX");
            DrawProp(editor, props, "_NoiseScrollY");

            EditorGUILayout.HelpBox(
                "추천 시작값:\n" +
                "마법: Scale 10~20 / Strength 0.2~0.4\n" +
                "전기: Scale 25~50 / Strength 0.5~0.9\n" +
                "연기: Scale 5~12 / Strength 0.4~0.7\n\n" +
                "Strength를 너무 높이면 텍스처가 지저분하게 깨져 보일 수 있습니다.",
                MessageType.Warning
            );

            DrawTex(editor, props, "_NoiseTex");
            DrawProp(editor, props, "_UseNoiseTex");
            DrawProp(editor, props, "_NoiseTexStrength");

            EditorGUILayout.HelpBox(
                "_NoiseTex: 직접 넣는 노이즈 텍스처\n" +
                "_UseNoiseTex: 노이즈 텍스처 사용 여부\n" +
                "_NoiseTexStrength: 절차적 노이즈 대신 텍스처 노이즈를 얼마나 섞을지\n\n" +
                "원하는 패턴이 명확하면 Noise Texture를 쓰는 편이 좋습니다.\n" +
                "단, 텍스처 샘플링이 추가되므로 비용은 조금 늘어납니다.",
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
            EditorGUILayout.HelpBox(
                "Mask는 특정 부분만 보이게 하거나 숨기는 기능입니다.\n\n" +
                "_MaskTex: 마스크 텍스처\n" +
                "_MaskStrength: 마스크 적용 강도\n" +
                "_InvertMask: 흰색/검은색 역할 반전",
                MessageType.Info
            );

            DrawTex(editor, props, "_MaskTex");
            DrawProp(editor, props, "_MaskStrength");
            DrawProp(editor, props, "_InvertMask");

            EditorGUILayout.HelpBox(
                "마스크 기본 규칙:\n" +
                "흰색 = 보임\n" +
                "검은색 = 숨김\n\n" +
                "Invert Mask를 켜면 반대로 작동합니다.\n" +
                "마법진, 검기 형태 제한, 특정 영역만 발광시키는 용도에 적합합니다.",
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
            EditorGUILayout.HelpBox(
                "Dissolve는 노이즈를 기준으로 이펙트가 부서지거나 사라지는 기능입니다.\n\n" +
                "_DissolveAmount: 사라지는 정도\n" +
                "_DissolveSoftness: 경계 부드러움\n" +
                "_DissolveEdgeColor: 경계 색\n" +
                "_DissolveEdgePower: 경계 발광 강도\n" +
                "_DissolveEdgeWidth: 경계 두께",
                MessageType.Info
            );

            DrawProp(editor, props, "_DissolveAmount");
            DrawProp(editor, props, "_DissolveSoftness");
            DrawProp(editor, props, "_DissolveEdgeColor");
            DrawProp(editor, props, "_DissolveEdgePower");
            DrawProp(editor, props, "_DissolveEdgeWidth");

            EditorGUILayout.HelpBox(
                "추천 용도:\n" +
                "- 마법 입자가 사라짐\n" +
                "- 검기 잔상이 찢어짐\n" +
                "- 폭발 파편이 부서짐\n\n" +
                "Particle System의 Alpha over Lifetime과 함께 쓰면 과하게 빨리 사라질 수 있으니 둘 중 하나를 먼저 조정하는 게 좋습니다.",
                MessageType.Warning
            );
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    static void DrawGradient(MaterialEditor editor, MaterialProperty[] props)
    {
        showGradient = EditorGUILayout.BeginFoldoutHeaderGroup(showGradient, "Gradient");
        if (showGradient)
        {
            EditorGUILayout.HelpBox(
                "Gradient는 색이 한 가지가 아니라 위치/노이즈/알파 기준으로 변하게 만드는 기능입니다.\n\n" +
                "_GradientStrength: 그라디언트 적용 강도\n" +
                "_GradientSource: 기준 선택\n" +
                "_GradientColorA/B/C: 3단 색상\n" +
                "_GradientOffset: 색 위치 이동\n" +
                "_GradientScale: 색 변화 압축/확대",
                MessageType.Info
            );

            DrawProp(editor, props, "_GradientStrength");
            DrawProp(editor, props, "_GradientSource");
            DrawProp(editor, props, "_GradientColorA");
            DrawProp(editor, props, "_GradientColorB");
            DrawProp(editor, props, "_GradientColorC");
            DrawProp(editor, props, "_GradientOffset");
            DrawProp(editor, props, "_GradientScale");

            EditorGUILayout.HelpBox(
                "Gradient Source:\n" +
                "0 = UV.y 세로 방향\n" +
                "1 = UV.x 가로 방향\n" +
                "2 = Noise 기준\n" +
                "3 = Alpha 기준\n\n" +
                "불꽃은 UV.y, 레이저/검기는 UV.x, 마법 노이즈는 Noise 기준이 잘 맞습니다.\n" +
                "Line/Trail은 UV 방향이 설정에 따라 달라질 수 있습니다.",
                MessageType.Warning
            );
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    static void DrawDistortion(MaterialEditor editor, MaterialProperty[] props)
    {
        showDistortion = EditorGUILayout.BeginFoldoutHeaderGroup(showDistortion, "UV Distortion");
        if (showDistortion)
        {
            EditorGUILayout.HelpBox(
                "UV Distortion은 텍스처 좌표를 노이즈로 흔드는 기능입니다.\n" +
                "오브젝트 자체가 흔들리는 게 아니라 텍스처 내부가 일렁입니다.\n\n" +
                "_DistortionStrength: 왜곡 강도\n" +
                "_DistortionScale: 왜곡 노이즈 크기\n" +
                "_DistortionScrollX/Y: 왜곡 패턴 이동",
                MessageType.Info
            );

            DrawProp(editor, props, "_DistortionStrength");
            DrawProp(editor, props, "_DistortionScale");
            DrawProp(editor, props, "_DistortionScrollX");
            DrawProp(editor, props, "_DistortionScrollY");

            EditorGUILayout.HelpBox(
                "추천 용도:\n" +
                "- 연기 일렁임\n" +
                "- 불꽃 흔들림\n" +
                "- 마력 흐름\n\n" +
                "Strength를 너무 높이면 텍스처가 찢어지거나 가장자리 샘플링이 어색할 수 있습니다. 0.01~0.08부터 테스트하세요.",
                MessageType.Warning
            );
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    static void DrawStyle(MaterialEditor editor, MaterialProperty[] props)
    {
        showStyle = EditorGUILayout.BeginFoldoutHeaderGroup(showStyle, "Style: Flicker / Pixel / Outline / Soft Edge");
        if (showStyle)
        {
            EditorGUILayout.HelpBox(
                "추가 스타일링 기능입니다. 이펙트의 성격을 빠르게 바꾸는 보조 기능들입니다.",
                MessageType.Info
            );

            DrawProp(editor, props, "_FlickerAmount");
            DrawProp(editor, props, "_FlickerSpeed");

            EditorGUILayout.HelpBox(
                "Flicker는 시간 기반 깜빡임입니다.\n\n" +
                "_FlickerAmount: 깜빡임 강도\n" +
                "_FlickerSpeed: 깜빡임 속도\n\n" +
                "전기, 불안정한 마법, 에너지 누출 같은 효과에 좋습니다.",
                MessageType.Info
            );

            DrawProp(editor, props, "_PixelAmount");

            EditorGUILayout.HelpBox(
                "Pixel Amount는 UV 기준 픽셀화입니다.\n" +
                "화면 전체를 픽셀화하는 후처리 효과가 아닙니다.\n\n" +
                "도트풍 텍스처 느낌을 보강하는 용도이며, 값이 0이면 꺼집니다.",
                MessageType.Warning
            );

            DrawProp(editor, props, "_OutlineWidth");
            DrawProp(editor, props, "_OutlineColor");
            DrawProp(editor, props, "_OutlinePower");

            EditorGUILayout.HelpBox(
                "Texture Outline은 텍스처 알파를 기준으로 만든 간이 외곽선입니다.\n\n" +
                "_OutlineWidth: 외곽선 두께\n" +
                "_OutlineColor: 외곽선 색\n" +
                "_OutlinePower: 외곽선 밝기\n\n" +
                "주의: Additive Blend에서는 선명한 외곽선보다 빛 번짐처럼 보일 수 있습니다.\n" +
                "또한 주변 픽셀을 추가 샘플링하므로 비용이 증가합니다.",
                MessageType.Warning
            );

            DrawProp(editor, props, "_SoftEdge");

            EditorGUILayout.HelpBox(
                "Soft Edge는 UV.y 기준으로 위아래 가장자리를 부드럽게 사라지게 합니다.\n\n" +
                "추천 대상: Line Renderer / Trail Renderer\n" +
                "비추천 대상: 일반 원형 파티클\n\n" +
                "일반 파티클에 쓰면 위아래가 눌리거나 의도치 않게 사라질 수 있습니다.",
                MessageType.Info
            );
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }
}