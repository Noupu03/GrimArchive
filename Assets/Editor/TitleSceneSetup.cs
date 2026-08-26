using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using Haare.Client.UI;

// RealBioSearch(외부 참고 프로젝트)의 타이틀 씬 구조(배경+제목+시작/설정/종료 버튼+버전 표기)를
// 이 프로젝트용으로 이식하는 1회성 에디터 도구. HaareUISetup.cs/SetupStatusInfoPanel.cs와 동일한
// 관례로, 프리팹/씬을 코드로 생성해서 저장한다 — 몇 번을 다시 실행해도 안전하다(항상 같은 경로에
// 덮어씀). 사용자가 Unity 에디터에서 "Tools/GrimArchive/타이틀 씬 생성"을 직접 실행해야 한다.
//
// 현재 열려있는 씬(작업 중인 씬)은 절대 건드리지 않는다 — Additive로 빈 씬을 하나 더 열어서 그
// 안에서만 Title.unity 내용을 만들고 저장한 뒤 닫는다.
public static class TitleSceneSetup
{
    private const string PrefabFolder = "Assets/Resources/Prefabs";
    private const string TitlePanelPrefabPath = PrefabFolder + "/GrimArchive_TitlePanel.prefab";
    private const string TitlePanelAddress = "Prefabs/GrimArchive_TitlePanel";
    private const string TitleScenePath = "Assets/Scenes/Title.unity";
    private const string SshScenePath = "Assets/Scenes/ssh.unity";
    private const string BackgroundImagePath = "Assets/Resources/Prefabs/그림2.png";

    // TMP 기본 폰트(LiberationSans SDF)엔 한글 글리프가 없어서 한글이 다 깨져 보인다.
    // 이미 프로젝트에 있는 한글 지원 폰트를 대신 쓴다(HaareUISetup.cs와 동일한 관례).
    private const string KoreanFontPath = "Assets/Haare/Fonts/NEXONLv1GothicBold SDF.asset";

    // RealBioSearch 타이틀 배경/텍스트 색상 그대로 재사용 (제목 문구만 프로젝트명으로 교체).
    private static readonly Color BackgroundColor = new Color(0.031f, 0.039f, 0.06f, 1f); // #080A0F
    private static readonly Color TitleColor = new Color(0.651f, 0.8f, 0.749f, 1f);        // #A6CCBF
    private static readonly Color ButtonLabelColor = new Color(0.851f, 0.898f, 0.878f, 1f); // #D9E5E0
    // 시인성 개선(2026-08-26, 사용자 신고 "타이틀 화면의 버튼, 글자 등이 너무 흐려서 잘 안보임") —
    // 배경 사진(그림2.png)이 밝고 화면 중앙이 특히 밝아서, 그 위에 얹는 옅은 색 텍스트/거의 안 보이는
    // 버튼(예전엔 흰색 8% 알파)이 배경에 묻혔다. 사진과 텍스트 사이에 어두운 스크림을 깔고, 버튼은
    // 불투명에 가까운 진한 배경으로 바꾸고, 텍스트엔 검은 아웃라인을 둘러서 어떤 배경 위에서도 읽히게
    // 한다.
    private static readonly Color ScrimColor = new Color(0f, 0f, 0f, 0.5f);
    private static readonly Color ButtonBackgroundColor = new Color(0f, 0f, 0f, 0.55f);
    private static readonly Color TextOutlineColor = new Color(0.02f, 0.02f, 0.03f, 1f);
    private const float TextOutlineWidth = 0.25f;

    [MenuItem("Tools/GrimArchive/타이틀 씬 생성")]
    public static void SetupTitleScene()
    {
        if (!Directory.Exists(PrefabFolder))
            Directory.CreateDirectory(PrefabFolder);

        GameObject panelPrefab = CreateTitlePanelPrefab();
        RegisterAddressable(panelPrefab, TitlePanelAddress);

        BuildAndSaveTitleScene();
        AddSceneToBuildSettings();

        Debug.Log("[TitleSceneSetup] 완료: GrimArchive_TitlePanel 프리팹 생성 + Addressable 등록, " +
                  "Title.unity 씬 생성/저장, Build Settings 등록(Title -> ssh)까지 마쳤습니다.");
    }

    // =====================================================
    // 1. 타이틀 패널 프리팹 (배경 + 제목 + 시작/설정/종료 버튼 + 버전 라벨)
    // =====================================================
    private static GameObject CreateTitlePanelPrefab()
    {
        var tmpResources = GetTMPResources();

        var root = new GameObject("TitlePanel", typeof(RectTransform));
        var rootRt = root.GetComponent<RectTransform>();
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.offsetMin = Vector2.zero;
        rootRt.offsetMax = Vector2.zero;

        // 배경 (전체 화면)
        var bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(root.transform, false);
        var bgRt = bg.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;
        var bgImage = bg.GetComponent<Image>();
        var backgroundSprite = AssetDatabase.LoadAssetAtPath<Sprite>(BackgroundImagePath);
        if (backgroundSprite != null)
        {
            bgImage.sprite = backgroundSprite;
            bgImage.color = Color.white;
            bgImage.type = Image.Type.Simple;
            bgImage.preserveAspect = false;
        }
        else
        {
            Debug.LogWarning($"[TitleSceneSetup] 배경 이미지를 찾지 못했습니다: {BackgroundImagePath} (기본 배경색으로 대체)");
            bgImage.color = BackgroundColor;
        }

        // 스크림(배경 사진과 글자/버튼 사이의 어두운 반투명 레이어) — 배경 사진 위, 글자/버튼 아래.
        var scrim = new GameObject("Scrim", typeof(RectTransform), typeof(Image));
        scrim.transform.SetParent(root.transform, false);
        var scrimRt = scrim.GetComponent<RectTransform>();
        scrimRt.anchorMin = Vector2.zero;
        scrimRt.anchorMax = Vector2.one;
        scrimRt.offsetMin = Vector2.zero;
        scrimRt.offsetMax = Vector2.zero;
        scrim.GetComponent<Image>().color = ScrimColor;

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
        titleTmp.text = "GRIM ARCHIVE";
        titleTmp.fontSize = 64;
        titleTmp.fontStyle = FontStyles.Bold;
        titleTmp.alignment = TextAlignmentOptions.Center;
        titleTmp.color = TitleColor;
        titleTmp.characterSpacing = 4;
        titleTmp.outlineWidth = TextOutlineWidth;
        titleTmp.outlineColor = TextOutlineColor;

        // 버튼 3종 (세로 스택 — RealBioSearch와 같은 y 배치)
        GameObject startGo = CreateTitleButton(tmpResources, "StartButton", "[ 시작 ]", new Vector2(0.5f, 0.48f));
        startGo.transform.SetParent(root.transform, false);

        GameObject settingsGo = CreateTitleButton(tmpResources, "SettingsButton", "[ 설정 ]", new Vector2(0.5f, 0.38f));
        settingsGo.transform.SetParent(root.transform, false);

        GameObject quitGo = CreateTitleButton(tmpResources, "QuitButton", "[ 종료 ]", new Vector2(0.5f, 0.28f));
        quitGo.transform.SetParent(root.transform, false);

        // 버전 라벨 (우하단)
        GameObject versionGo = TMP_DefaultControls.CreateText(tmpResources);
        versionGo.name = "VersionLabel";
        versionGo.transform.SetParent(root.transform, false);
        var versionRt = versionGo.GetComponent<RectTransform>();
        versionRt.anchorMin = new Vector2(1, 0);
        versionRt.anchorMax = new Vector2(1, 0);
        versionRt.pivot = new Vector2(1, 0);
        versionRt.anchoredPosition = new Vector2(-10, 10);
        versionRt.sizeDelta = new Vector2(160, 24);
        var versionTmp = versionGo.GetComponent<TextMeshProUGUI>();
        versionTmp.font = GetKoreanFont();
        versionTmp.fontSize = 14;
        versionTmp.color = new Color(0.5f, 0.5f, 0.5f, 1f);
        versionTmp.alignment = TextAlignmentOptions.BottomRight;
        versionTmp.text = "v" + Application.version;

        var titlePanel = root.AddComponent<GameTitlePanel>();
        var so = new SerializedObject(titlePanel);
        so.FindProperty("StartButton").objectReferenceValue = startGo.GetComponent<CustomButton>();
        so.FindProperty("SettingsButton").objectReferenceValue = settingsGo.GetComponent<CustomButton>();
        so.FindProperty("QuitButton").objectReferenceValue = quitGo.GetComponent<CustomButton>();
        so.ApplyModifiedPropertiesWithoutUndo();

        if (File.Exists(TitlePanelPrefabPath))
            AssetDatabase.DeleteAsset(TitlePanelPrefabPath);

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, TitlePanelPrefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static GameObject CreateTitleButton(TMP_DefaultControls.Resources tmpResources, string name, string label, Vector2 anchor)
    {
        GameObject go = TMP_DefaultControls.CreateButton(tmpResources);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Button>());

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(340, 64);
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

    // =====================================================
    // 2. Title.unity 씬 (Canvas + EventSystem + DI 루트)
    // =====================================================
    private static void BuildAndSaveTitleScene()
    {
        // 현재 열려있는 씬은 그대로 두고, Additive로 빈 씬을 하나 더 열어서 그 안에서만 작업한다.
        Scene newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

        var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        camGo.tag = "MainCamera";
        var cam = camGo.GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = BackgroundColor;
        cam.orthographic = true;

        var esGo = new GameObject("EventSystem", typeof(EventSystem));
        var uiModule = esGo.AddComponent<InputSystemUIInputModule>();
        WireDefaultUIActions(uiModule);

        var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        var safeArea = new GameObject("SafeArea", typeof(RectTransform));
        safeArea.transform.SetParent(canvasGo.transform, false);
        var safeRt = safeArea.GetComponent<RectTransform>();
        safeRt.anchorMin = Vector2.zero;
        safeRt.anchorMax = Vector2.one;
        safeRt.offsetMin = Vector2.zero;
        safeRt.offsetMax = Vector2.zero;

        var titleUIManager = canvasGo.AddComponent<TitleUIManager>();
        var uiManagerSo = new SerializedObject(titleUIManager);
        uiManagerSo.FindProperty("safePannelRect").objectReferenceValue = safeRt;
        uiManagerSo.ApplyModifiedPropertiesWithoutUndo();

        // DI 루트 (SceneUIManager + Presenter 등록만 — ssh.unity의 GameCompositionRoot와 달리
        // CoreLifetimeScope를 상속하지 않는다. CoreUIManager를 여기서도 DontDestroyOnLoad로 새로
        // 만들면 SceneManager.LoadScene("ssh")로 전환할 때 ssh.unity의 GameCompositionRoot가 만드는
        // 것과 중복 등록될 위험이 있어, Title 전용 SceneUIManager만으로 최소한으로 구성했다).
        new GameObject("TitleCompositionRoot", typeof(TitleScope));

        EditorSceneManager.MarkSceneDirty(newScene);
        EditorSceneManager.SaveScene(newScene, TitleScenePath);
        EditorSceneManager.CloseScene(newScene, true);
    }

    // ssh.unity의 EventSystem이 쓰는 것과 같은 계열의 Input Actions 에셋(UI 액션맵 보유)을 찾아서
    // InputSystemUIInputModule에 연결한다. 프로젝트에 여러 개가 있으면 첫 번째로 찾은 것을 쓴다.
    private static void WireDefaultUIActions(InputSystemUIInputModule module)
    {
        InputActionAsset actionsAsset = null;
        foreach (var guid in AssetDatabase.FindAssets("t:InputActionAsset"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(path);
            if (asset != null && asset.FindActionMap("UI", false) != null)
            {
                actionsAsset = asset;
                break;
            }
        }

        if (actionsAsset == null)
        {
            Debug.LogWarning("[TitleSceneSetup] UI 액션맵을 가진 InputActionAsset을 찾지 못했습니다. " +
                              "Title.unity의 EventSystem에서 InputSystemUIInputModule을 수동으로 설정해주세요.");
            return;
        }

        var uiMap = actionsAsset.FindActionMap("UI", false);
        module.actionsAsset = actionsAsset;
        module.point = CreateRef(uiMap.FindAction("Point", false));
        module.leftClick = CreateRef(uiMap.FindAction("Click", false));
        module.middleClick = CreateRef(uiMap.FindAction("MiddleClick", false));
        module.rightClick = CreateRef(uiMap.FindAction("RightClick", false));
        module.scrollWheel = CreateRef(uiMap.FindAction("ScrollWheel", false));
        module.move = CreateRef(uiMap.FindAction("Navigate", false));
        module.submit = CreateRef(uiMap.FindAction("Submit", false));
        module.cancel = CreateRef(uiMap.FindAction("Cancel", false));
    }

    private static InputActionReference CreateRef(InputAction action) =>
        action != null ? InputActionReference.Create(action) : null;

    // =====================================================
    // 3. Build Settings — Title을 0번, ssh를 그 다음으로 등록 (기존 목록에 이미 있으면 유지)
    // =====================================================
    private static void AddSceneToBuildSettings()
    {
        var existing = EditorBuildSettings.scenes;
        var result = new List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(TitleScenePath, true) };

        bool hasSsh = false;
        foreach (var s in existing)
        {
            if (s.path == TitleScenePath) continue; // 중복 방지 (맨 앞에 이미 추가함)
            if (s.path == SshScenePath) hasSsh = true;
            result.Add(s);
        }
        if (!hasSsh)
            result.Add(new EditorBuildSettingsScene(SshScenePath, true));

        EditorBuildSettings.scenes = result.ToArray();
    }

    private static void RegisterAddressable(GameObject prefab, string address)
    {
        string assetPath = AssetDatabase.GetAssetPath(prefab);
        string guid = AssetDatabase.AssetPathToGUID(assetPath);

        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
        {
            Debug.LogError("[TitleSceneSetup] AddressableAssetSettings를 찾을 수 없습니다. Addressables 초기 설정을 먼저 확인하세요.");
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
                Debug.LogError($"[TitleSceneSetup] 한글 폰트를 찾을 수 없습니다: {KoreanFontPath} (TMP 기본 폰트로 대체되어 한글이 깨질 수 있음)");
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
