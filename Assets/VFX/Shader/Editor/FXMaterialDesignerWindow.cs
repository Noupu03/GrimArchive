using UnityEditor;
using UnityEngine;

public class FXMaterialDesignerWindow : EditorWindow
{
    private enum TargetType { Particle, Line, Trail }
    private enum StylePreset { Custom, Fire, Magic, Electric, Smoke, SlashTrail }

    private TargetType targetType = TargetType.Particle;
    private StylePreset stylePreset = StylePreset.Magic;

    private string materialName = "M_FX_New";
    private Color tintColor = Color.white;
    private Color emissionColor = Color.white;

    private float alpha = 1f;
    private float emissionPower = 4f;
    private float noiseStrength = 0.3f;
    private float noiseScale = 12f;
    private float scrollX = 0f;
    private float scrollY = 1f;
    private float dissolveAmount = 0f;
    private float distortionStrength = 0f;
    private float softEdge = 0f;

    private bool useAdditive = true;
    private bool useNoise = true;
    private bool useDissolve = false;
    private bool useDistortion = false;
    private bool useSoftEdge = false;

    private const string ShaderName = "Custom/FX/URP_AllInOne_Lab_v02";

    [MenuItem("Tools/FX/FX Material Designer")]
    public static void Open()
    {
        GetWindow<FXMaterialDesignerWindow>("FX Material Designer");
    }

    private void OnGUI()
    {
        EditorGUILayout.Space();

        EditorGUILayout.HelpBox(
            "FX Material Designer\n" +
            "All-in-One 실험용 셰이더를 기반으로 파티클/라인/트레일용 머티리얼을 빠르게 생성합니다.",
            MessageType.Info
        );

        materialName = EditorGUILayout.TextField("Material Name", materialName);
        targetType = (TargetType)EditorGUILayout.EnumPopup("Target Type", targetType);
        stylePreset = (StylePreset)EditorGUILayout.EnumPopup("Style Preset", stylePreset);

        if (GUILayout.Button("Apply Preset To Values"))
        {
            ApplyPreset();
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Main", EditorStyles.boldLabel);

        tintColor = EditorGUILayout.ColorField("Tint Color", tintColor);
        emissionColor = EditorGUILayout.ColorField("Emission Color", emissionColor);
        alpha = EditorGUILayout.Slider("Alpha", alpha, 0f, 1f);
        emissionPower = EditorGUILayout.Slider("Emission Power", emissionPower, 0f, 30f);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Modules", EditorStyles.boldLabel);

        useAdditive = EditorGUILayout.Toggle("Additive Glow", useAdditive);
        useNoise = EditorGUILayout.Toggle("Noise", useNoise);
        useDissolve = EditorGUILayout.Toggle("Dissolve", useDissolve);
        useDistortion = EditorGUILayout.Toggle("Distortion", useDistortion);
        useSoftEdge = EditorGUILayout.Toggle("Soft Edge", useSoftEdge);

        if (useNoise)
        {
            noiseStrength = EditorGUILayout.Slider("Noise Strength", noiseStrength, 0f, 1f);
            noiseScale = EditorGUILayout.Slider("Noise Scale", noiseScale, 0.1f, 100f);
        }

        scrollX = EditorGUILayout.FloatField("Scroll X", scrollX);
        scrollY = EditorGUILayout.FloatField("Scroll Y", scrollY);

        if (useDissolve)
        {
            dissolveAmount = EditorGUILayout.Slider("Dissolve Amount", dissolveAmount, 0f, 1f);
        }

        if (useDistortion)
        {
            distortionStrength = EditorGUILayout.Slider("Distortion Strength", distortionStrength, 0f, 0.3f);
        }

        if (useSoftEdge)
        {
            softEdge = EditorGUILayout.Slider("Soft Edge", softEdge, 0f, 1f);
        }

        EditorGUILayout.Space();

        if (GUILayout.Button("Create Material", GUILayout.Height(32)))
        {
            CreateMaterial();
        }

        EditorGUILayout.Space();

        EditorGUILayout.HelpBox(
            "생성 후 머티리얼을 Particle System / Line Renderer / Trail Renderer의 Material 슬롯에 넣으면 됩니다.\n\n" +
            "이 툴은 최종 최적화용이 아니라, 빠르게 머티리얼 프리셋을 찍어내는 실험용입니다.",
            MessageType.Warning
        );
    }

    private void ApplyPreset()
    {
        switch (stylePreset)
        {
            case StylePreset.Fire:
                materialName = "M_FX_Fire";
                tintColor = new Color(1f, 0.35f, 0.05f, 1f);
                emissionColor = new Color(1f, 0.45f, 0.1f, 1f);
                alpha = 0.85f;
                emissionPower = 6f;
                noiseStrength = 0.55f;
                noiseScale = 18f;
                scrollX = 0f;
                scrollY = 1.8f;
                useAdditive = true;
                useNoise = true;
                useDissolve = false;
                useDistortion = true;
                distortionStrength = 0.03f;
                useSoftEdge = false;
                break;

            case StylePreset.Magic:
                materialName = "M_FX_Magic";
                tintColor = new Color(0.25f, 0.55f, 1f, 1f);
                emissionColor = new Color(0.35f, 0.7f, 1f, 1f);
                alpha = 0.9f;
                emissionPower = 5f;
                noiseStrength = 0.35f;
                noiseScale = 14f;
                scrollX = 0.2f;
                scrollY = 0.6f;
                useAdditive = true;
                useNoise = true;
                useDissolve = true;
                dissolveAmount = 0.1f;
                useDistortion = false;
                useSoftEdge = false;
                break;

            case StylePreset.Electric:
                materialName = "M_FX_Electric";
                tintColor = new Color(0.45f, 0.8f, 1f, 1f);
                emissionColor = new Color(0.5f, 0.9f, 1f, 1f);
                alpha = 1f;
                emissionPower = 9f;
                noiseStrength = 0.8f;
                noiseScale = 35f;
                scrollX = 2.5f;
                scrollY = 0f;
                useAdditive = true;
                useNoise = true;
                useDissolve = false;
                useDistortion = true;
                distortionStrength = 0.04f;
                useSoftEdge = true;
                softEdge = 0.25f;
                break;

            case StylePreset.Smoke:
                materialName = "M_FX_Smoke";
                tintColor = new Color(0.45f, 0.45f, 0.45f, 0.6f);
                emissionColor = Color.white;
                alpha = 0.45f;
                emissionPower = 1f;
                noiseStrength = 0.6f;
                noiseScale = 8f;
                scrollX = 0.05f;
                scrollY = 0.25f;
                useAdditive = false;
                useNoise = true;
                useDissolve = false;
                useDistortion = true;
                distortionStrength = 0.05f;
                useSoftEdge = false;
                break;

            case StylePreset.SlashTrail:
                materialName = "M_FX_SlashTrail";
                tintColor = new Color(0.8f, 0.95f, 1f, 1f);
                emissionColor = new Color(0.8f, 0.95f, 1f, 1f);
                alpha = 0.9f;
                emissionPower = 5f;
                noiseStrength = 0.25f;
                noiseScale = 20f;
                scrollX = 1.5f;
                scrollY = 0f;
                useAdditive = true;
                useNoise = true;
                useDissolve = true;
                dissolveAmount = 0.05f;
                useDistortion = false;
                useSoftEdge = true;
                softEdge = 0.35f;
                break;
        }
    }

    private void CreateMaterial()
    {
        Shader shader = Shader.Find(ShaderName);

        if (shader == null)
        {
            EditorUtility.DisplayDialog(
                "Shader Not Found",
                $"셰이더를 찾을 수 없습니다.\n\n필요한 셰이더 이름:\n{ShaderName}",
                "OK"
            );
            return;
        }

        string folder = "Assets/Materials/FX";

        if (!AssetDatabase.IsValidFolder("Assets/Materials"))
        {
            AssetDatabase.CreateFolder("Assets", "Materials");
        }

        if (!AssetDatabase.IsValidFolder(folder))
        {
            AssetDatabase.CreateFolder("Assets/Materials", "FX");
        }

        Material mat = new Material(shader);
        mat.name = materialName;

        ApplyValuesToMaterial(mat);

        string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{materialName}.mat");
        AssetDatabase.CreateAsset(mat, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Selection.activeObject = mat;

        EditorUtility.DisplayDialog(
            "Material Created",
            $"머티리얼 생성 완료:\n{path}",
            "OK"
        );
    }

    private void ApplyValuesToMaterial(Material mat)
    {
        Set(mat, "_TintColor", tintColor);
        Set(mat, "_EmissionColor", emissionColor);
        Set(mat, "_Alpha", alpha);
        Set(mat, "_EmissionPower", emissionPower);

        Set(mat, "_UseVertexColor", 1f);

        Set(mat, "_MainScrollX", scrollX);
        Set(mat, "_MainScrollY", scrollY);

        Set(mat, "_NoiseStrength", useNoise ? noiseStrength : 0f);
        Set(mat, "_NoiseScale", noiseScale);

        Set(mat, "_DissolveAmount", useDissolve ? dissolveAmount : 0f);
        Set(mat, "_DistortionStrength", useDistortion ? distortionStrength : 0f);
        Set(mat, "_SoftEdge", useSoftEdge ? softEdge : 0f);

        if (useAdditive)
        {
            Set(mat, "_SrcBlend", 5f); // SrcAlpha
            Set(mat, "_DstBlend", 1f); // One
        }
        else
        {
            Set(mat, "_SrcBlend", 5f);  // SrcAlpha
            Set(mat, "_DstBlend", 10f); // OneMinusSrcAlpha
        }

        Set(mat, "_Cull", 0f);
        Set(mat, "_ZWrite", 0f);

        if (targetType == TargetType.Line || targetType == TargetType.Trail)
        {
            Set(mat, "_SoftEdge", useSoftEdge ? softEdge : 0.25f);
        }
    }

    private void Set(Material mat, string property, float value)
    {
        if (mat.HasProperty(property))
        {
            mat.SetFloat(property, value);
        }
    }

    private void Set(Material mat, string property, Color value)
    {
        if (mat.HasProperty(property))
        {
            mat.SetColor(property, value);
        }
    }
}