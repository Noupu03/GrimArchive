using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Haare.Client.UI;

// 키 가이드 패널(Assets/Script/UI/KeyGuide/KeyGuidePanel.cs) 프리팹을 코드로 생성하는 1회성 에디터
// 도구(2026-08-26, 사용자 요청 "타이틀과 esc에 키 가이드 항목 넣어줘. 실제 사용 키에 대한 가이드
// 설명 패널 뜨게 해주고"). TitleSceneSetup.cs/SettingsPanelSetup.cs와 동일한 관례 — 배경색/글자색/
// 버튼 스타일을 그대로 재사용한다. 실제 키 목록은 이 게임의 유일한 입력 표면인
// Assets/Script/Unit/Session/GameInputScheme.cs를 그대로 옮겨적은 것이다 — 입력이 바뀌면 아래
// KeyGuideBodyText도 같이 갱신하고 이 메뉴를 다시 실행해야 한다. 사용자가 Unity 에디터에서
// "Tools/GrimArchive/키 가이드 패널 생성"을 직접 실행해야 한다.
public static class KeyGuidePanelSetup
{
    private const string PrefabFolder = "Assets/Resources/Prefabs";
    private const string PrefabPath = PrefabFolder + "/GrimArchive_KeyGuidePanel.prefab";
    private const string Address = "Prefabs/GrimArchive_KeyGuidePanel";

    private const string KoreanFontPath = "Assets/Haare/Fonts/NEXONLv1GothicBold SDF.asset";

    // TitleSceneSetup.cs/SettingsPanelSetup.cs와 동일한 배색.
    private static readonly Color BackgroundColor = new Color(0.031f, 0.039f, 0.06f, 0.92f); // #080A0F
    private static readonly Color TitleColor = new Color(0.651f, 0.8f, 0.749f, 1f);           // #A6CCBF
    private static readonly Color ButtonLabelColor = new Color(0.851f, 0.898f, 0.878f, 1f);   // #D9E5E0
    private static readonly Color ButtonBackgroundColor = new Color(0f, 0f, 0f, 0.55f);
    private static readonly Color TextOutlineColor = new Color(0.02f, 0.02f, 0.03f, 1f);
    private const float TextOutlineWidth = 0.25f;

    // GameInputScheme.cs 그대로(2026-08-26 기준) — WASD/휠/좌클릭/Ctrl/우클릭/Space/1~4/ESC.
    private const string KeyGuideBodyText =
        "<b>W A S D</b>  —  카메라 이동\n" +
        "<b>마우스 휠</b>  —  화면 확대 / 축소\n" +
        "<b>좌클릭</b>  —  유닛 선택 (드래그: 범위 선택)\n" +
        "<b>Ctrl + 좌클릭</b>  —  선택에 추가\n" +
        "<b>우클릭</b>  —  이동 / 공격 / 설치 확정 등 실행\n" +
        "<b>Space</b>  —  일시정지\n" +
        "<b>1 / 2 / 3 / 4</b>  —  게임 속도 0.5x / 1.0x / 1.5x / 2.0x\n" +
        "<b>Esc</b>  —  설정 패널 열기 / 닫기";

    [MenuItem("Tools/GrimArchive/키 가이드 패널 생성")]
    public static void SetupKeyGuidePanel()
    {
        if (!Directory.Exists(PrefabFolder))
            Directory.CreateDirectory(PrefabFolder);

        GameObject prefab = CreateKeyGuidePanelPrefab();
        RegisterAddressable(prefab, Address);

        Debug.Log("[KeyGuidePanelSetup] 완료: GrimArchive_KeyGuidePanel 프리팹 생성 + Addressable 등록까지 마쳤습니다.");
    }

    private static GameObject CreateKeyGuidePanelPrefab()
    {
        var tmpResources = GetTMPResources();

        var root = new GameObject("KeyGuidePanel", typeof(RectTransform));
        var rootRt = root.GetComponent<RectTransform>();
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.offsetMin = Vector2.zero;
        rootRt.offsetMax = Vector2.zero;

        // 배경 (전체 화면 — 뒤에 숨겨진 타이틀/설정 화면을 덮는다)
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
        titleRt.anchorMin = new Vector2(0.5f, 0.82f);
        titleRt.anchorMax = new Vector2(0.5f, 0.82f);
        titleRt.pivot = new Vector2(0.5f, 0.5f);
        titleRt.sizeDelta = new Vector2(900, 100);
        titleRt.anchoredPosition = Vector2.zero;
        var titleTmp = titleGo.GetComponent<TextMeshProUGUI>();
        titleTmp.font = GetKoreanFont();
        titleTmp.text = "키 가이드";
        titleTmp.fontSize = 48;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.alignment = TextAlignmentOptions.Center;
        titleTmp.color = TitleColor;
        titleTmp.characterSpacing = 4;
        titleTmp.outlineWidth = TextOutlineWidth;
        titleTmp.outlineColor = TextOutlineColor;

        // 본문(키 목록) — 왼쪽 정렬, 화면 중앙에 고정폭 박스로 배치.
        GameObject bodyGo = TMP_DefaultControls.CreateText(tmpResources);
        bodyGo.name = "BodyText";
        bodyGo.transform.SetParent(root.transform, false);
        var bodyRt = bodyGo.GetComponent<RectTransform>();
        bodyRt.anchorMin = new Vector2(0.5f, 0.5f);
        bodyRt.anchorMax = new Vector2(0.5f, 0.5f);
        bodyRt.pivot = new Vector2(0.5f, 0.5f);
        bodyRt.sizeDelta = new Vector2(720, 440);
        bodyRt.anchoredPosition = new Vector2(0, 10);
        var bodyTmp = bodyGo.GetComponent<TextMeshProUGUI>();
        bodyTmp.font = GetKoreanFont();
        bodyTmp.text = KeyGuideBodyText;
        bodyTmp.fontSize = 26;
        bodyTmp.lineSpacing = 24;
        bodyTmp.alignment = TextAlignmentOptions.TopLeft;
        bodyTmp.color = ButtonLabelColor;
        bodyTmp.outlineWidth = TextOutlineWidth;
        bodyTmp.outlineColor = TextOutlineColor;

        // 닫기 버튼
        GameObject closeGo = CreateKeyGuideButton(tmpResources, "CloseButton", "[ 닫기 ]", new Vector2(0.5f, 0.1f));
        closeGo.transform.SetParent(root.transform, false);

        var keyGuidePanel = root.AddComponent<KeyGuidePanel>();
        var so = new SerializedObject(keyGuidePanel);
        so.FindProperty("CloseButton").objectReferenceValue = closeGo.GetComponent<CustomButton>();
        so.ApplyModifiedPropertiesWithoutUndo();

        if (File.Exists(PrefabPath))
            AssetDatabase.DeleteAsset(PrefabPath);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static GameObject CreateKeyGuideButton(TMP_DefaultControls.Resources tmpResources, string name, string label, Vector2 anchor)
    {
        GameObject go = TMP_DefaultControls.CreateButton(tmpResources);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Button>());

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(280, 64);
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
            Debug.LogError("[KeyGuidePanelSetup] AddressableAssetSettings를 찾을 수 없습니다. Addressables 초기 설정을 먼저 확인하세요.");
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
                Debug.LogError($"[KeyGuidePanelSetup] 한글 폰트를 찾을 수 없습니다: {KoreanFontPath} (TMP 기본 폰트로 대체되어 한글이 깨질 수 있음)");
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
