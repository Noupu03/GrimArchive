using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Haare.Client.UI;

// ESC 설정 패널(Assets/Script/UI/Settings/GameSettingsPanel.cs) 프리팹을 코드로 생성하는 1회성 에디터
// 도구(2026-08-26, 사용자 요청 "esc를 누르면... 옵션 panel이 뜨게 해줘, 옵션의 기능들은 아직
// 미구현이니 타이틀처럼 화면만 두고, 타이틀 화면으로 나가기를 추가해줘" → 이후 "옵션이라는 말, 설정으로
// 통일해"). TitleSceneSetup.cs와 동일한 관례 — 배경색/글자색/버튼 스타일을 그대로 재사용해 Title.unity와
// 시각적으로 통일된 설정 패널을 만든다. 실제로 동작하는 버튼은 "게임으로 돌아가기"(재개)/"타이틀
// 화면으로 나가기" 둘뿐이고, 그 외 설정 항목은 아직 없어 자리표시 문구만 둔다. 몇 번을 다시 실행해도
// 안전하다(항상 같은 경로에 덮어씀). 사용자가 Unity 에디터에서 "Tools/GrimArchive/설정 패널 생성"을
// 직접 실행해야 한다.
public static class SettingsPanelSetup
{
    private const string PrefabFolder = "Assets/Resources/Prefabs";
    private const string PrefabPath = PrefabFolder + "/GrimArchive_SettingsPanel.prefab";
    private const string Address = "Prefabs/GrimArchive_SettingsPanel";

    // TMP 기본 폰트(LiberationSans SDF)엔 한글 글리프가 없어서 한글이 다 깨져 보인다. TitleSceneSetup.cs와
    // 동일한 한글 지원 폰트를 재사용한다.
    private const string KoreanFontPath = "Assets/Haare/Fonts/NEXONLv1GothicBold SDF.asset";

    // TitleSceneSetup.cs의 RealBioSearch 배색을 그대로 재사용(배경만 완전 불투명 대신 살짝 투명하게 —
    // 일시정지된 게임 화면 위에 뜨는 오버레이라는 걸 알 수 있게).
    private static readonly Color BackgroundColor = new Color(0.031f, 0.039f, 0.06f, 0.92f); // #080A0F
    private static readonly Color TitleColor = new Color(0.651f, 0.8f, 0.749f, 1f);           // #A6CCBF
    private static readonly Color ButtonLabelColor = new Color(0.851f, 0.898f, 0.878f, 1f);   // #D9E5E0
    // Title 화면 시인성 개선(2026-08-26, 사용자 신고 "버튼, 글자 등이 너무 흐려서 잘 안보임")과 동일한
    // 값 — TitleSceneSetup.cs 참고. 이 패널은 원래도 불투명에 가까운 단색 배경이라 문제가 덜했지만,
    // 두 화면의 버튼이 서로 다르게 보이지 않도록 스타일을 맞춘다.
    private static readonly Color ButtonBackgroundColor = new Color(0f, 0f, 0f, 0.55f);
    private static readonly Color TextOutlineColor = new Color(0.02f, 0.02f, 0.03f, 1f);
    private const float TextOutlineWidth = 0.25f;

    [MenuItem("Tools/GrimArchive/설정 패널 생성")]
    public static void SetupSettingsPanel()
    {
        if (!Directory.Exists(PrefabFolder))
            Directory.CreateDirectory(PrefabFolder);

        GameObject prefab = CreateSettingsPanelPrefab();
        RegisterAddressable(prefab, Address);

        Debug.Log("[SettingsPanelSetup] 완료: GrimArchive_SettingsPanel 프리팹 생성 + Addressable 등록까지 마쳤습니다.");
    }

    private static GameObject CreateSettingsPanelPrefab()
    {
        var tmpResources = GetTMPResources();

        var root = new GameObject("SettingsPanel", typeof(RectTransform));
        var rootRt = root.GetComponent<RectTransform>();
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.offsetMin = Vector2.zero;
        rootRt.offsetMax = Vector2.zero;

        // 배경 (전체 화면 — 일시정지된 게임 화면을 덮는다)
        var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(root.transform, false);
        var bgRt = bg.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;
        bg.GetComponent<Image>().color = BackgroundColor;

        // 제목 텍스트
        GameObject titleGo = TMP_DefaultControls.CreateText(tmpResources);
        titleGo.name = "TitleText";
        titleGo.transform.SetParent(root.transform, false);
        var titleRt = titleGo.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0.5f, 0.68f);
        titleRt.anchorMax = new Vector2(0.5f, 0.68f);
        titleRt.pivot = new Vector2(0.5f, 0.5f);
        titleRt.sizeDelta = new Vector2(900, 140);
        titleRt.anchoredPosition = Vector2.zero;
        var titleTmp = titleGo.GetComponent<TextMeshProUGUI>();
        titleTmp.font = GetKoreanFont();
        titleTmp.text = "설정";
        titleTmp.fontSize = 64;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.alignment = TextAlignmentOptions.Center;
        titleTmp.color = TitleColor;
        titleTmp.characterSpacing = 4;
        titleTmp.outlineWidth = TextOutlineWidth;
        titleTmp.outlineColor = TextOutlineColor;

        // 부제 — 아직 실제 설정 항목이 없다는 안내(자리표시).
        GameObject subGo = TMP_DefaultControls.CreateText(tmpResources);
        subGo.name = "SubText";
        subGo.transform.SetParent(root.transform, false);
        var subRt = subGo.GetComponent<RectTransform>();
        subRt.anchorMin = new Vector2(0.5f, 0.6f);
        subRt.anchorMax = new Vector2(0.5f, 0.6f);
        subRt.pivot = new Vector2(0.5f, 0.5f);
        subRt.sizeDelta = new Vector2(900, 60);
        subRt.anchoredPosition = Vector2.zero;
        var subTmp = subGo.GetComponent<TextMeshProUGUI>();
        subTmp.font = GetKoreanFont();
        subTmp.text = "(준비 중)";
        subTmp.fontSize = 24;
        subTmp.alignment = TextAlignmentOptions.Center;
        subTmp.color = ButtonLabelColor;
        subTmp.outlineWidth = TextOutlineWidth;
        subTmp.outlineColor = TextOutlineColor;

        // 버튼 2종 (세로 스택 — Title 버튼과 같은 배치 방식)
        GameObject resumeGo = CreateSettingsButton(tmpResources, "ResumeButton", "[ 게임으로 돌아가기 ]", new Vector2(0.5f, 0.42f));
        resumeGo.transform.SetParent(root.transform, false);

        GameObject quitGo = CreateSettingsButton(tmpResources, "QuitToTitleButton", "[ 타이틀 화면으로 나가기 ]", new Vector2(0.5f, 0.32f));
        quitGo.transform.SetParent(root.transform, false);

        var settingsPanel = root.AddComponent<GameSettingsPanel>();
        var so = new SerializedObject(settingsPanel);
        so.FindProperty("ResumeButton").objectReferenceValue = resumeGo.GetComponent<CustomButton>();
        so.FindProperty("QuitToTitleButton").objectReferenceValue = quitGo.GetComponent<CustomButton>();
        so.ApplyModifiedPropertiesWithoutUndo();

        if (File.Exists(PrefabPath))
            AssetDatabase.DeleteAsset(PrefabPath);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static GameObject CreateSettingsButton(TMP_DefaultControls.Resources tmpResources, string name, string label, Vector2 anchor)
    {
        GameObject go = TMP_DefaultControls.CreateButton(tmpResources);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Button>());

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(400, 64);
        rt.anchoredPosition = Vector2.zero;

        var image = go.GetComponent<Image>();
        if (image != null) image.color = ButtonBackgroundColor;
        go.AddComponent<CustomImage>();
        go.AddComponent<CustomButton>();

        var textGo = go.transform.Find("Text (TMP)").gameObject;
        var buttonTmp = textGo.GetComponent<TextMeshProUGUI>();
        buttonTmp.font = GetKoreanFont();
        buttonTmp.text = label;
        buttonTmp.color = ButtonLabelColor;
        buttonTmp.fontSize = 24;
        buttonTmp.outlineWidth = TextOutlineWidth;
        buttonTmp.outlineColor = TextOutlineColor;
        textGo.AddComponent<CustomText>();

        return go;
    }

    private static void RegisterAddressable(GameObject prefab, string address)
    {
        string assetPath = AssetDatabase.GetAssetPath(prefab);
        string guid = AssetDatabase.AssetPathToGUID(assetPath);

        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogError("[SettingsPanelSetup] AddressableAssetSettings를 찾을 수 없습니다. Addressables 초기 설정을 먼저 확인하세요.");
            return;
        }

        var group = settings.FindGroup("Default Local Group") ?? settings.DefaultGroup;
        var entry = settings.CreateOrMoveEntry(guid, group);
        entry.address = address;

        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
    }

    private static Sprite GetBuiltinSprite(string path) => AssetDatabase.GetBuiltinExtraResource<Sprite>(path);

    private static TMP_FontAsset _koreanFont;
    private static TMP_FontAsset GetKoreanFont()
    {
        if (_koreanFont == null)
        {
            _koreanFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(KoreanFontPath);
            if (_koreanFont == null)
                Debug.LogError($"[SettingsPanelSetup] 한글 폰트를 찾을 수 없습니다: {KoreanFontPath} (TMP 기본 폰트로 대체되어 한글이 깨질 수 있음)");
        }
        return _koreanFont;
    }

    private static TMP_DefaultControls.Resources GetTMPResources()
    {
        return new TMP_DefaultControls.Resources
        {
            standard = GetBuiltinSprite("UI/Skin/UISprite.psd"),
            background = GetBuiltinSprite("UI/Skin/Background.psd"),
            inputField = GetBuiltinSprite("UI/Skin/InputFieldBackground.psd"),
            knob = GetBuiltinSprite("UI/Skin/Knob.psd"),
            checkmark = GetBuiltinSprite("UI/Skin/Checkmark.psd"),
            dropdown = GetBuiltinSprite("UI/Skin/DropdownArrow.psd"),
            mask = GetBuiltinSprite("UI/Skin/UIMask.psd"),
        };
    }
}
