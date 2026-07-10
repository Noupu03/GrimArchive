using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using Haare.Client.UI;

// Haare CoreUIManager용 Canvas 프리팹과 DebugInfoPanel 프리팹을 코드로 생성하고,
// Addressable 등록 + ssh.unity의 CompositionRoot 배선까지 한 번에 처리하는 1회성 에디터 도구.
// JsonToUnitPrefabConverter.cs와 동일하게 몇 번을 다시 실행해도 안전하다(항상 같은 경로에 덮어씀).
public static class HaareUISetup
{
    private const string OutputFolder = "Assets/Prefabs/UI";
    private const string CoreCanvasAddress = "Prefabs/CoreCanvas";
    private const string DebugPanelAddress = "Prefabs/DebugInfoPanel";
    // Haare.Client.Core.DI.UIPresenter.FadeIn()/FadeOut()이 Demo.UI.LoadingFadePanel을 그 클래스에
    // 박힌 [PanelAttribute] 주소로 직접 로드하기 때문에, 우리 프리팹도 이 주소 그대로 등록해야 한다.
    private const string LoadingFadePanelAddress = "Prefabs/Demo_LoadingFadePanel";
    private const string ScenePath = "Assets/Scenes/ssh.unity";

    // TMP 기본 폰트(LiberationSans SDF)엔 한글 글리프가 없어서 한글이 다 깨져 보인다.
    // 이미 프로젝트에 있는 한글 지원 폰트(Dynamic 아틀라스라 런타임에 필요한 글자만 채워짐)를 대신 쓴다.
    private const string KoreanFontPath = "Assets/Haare/Fonts/NEXONLv1GothicRegular SDF.asset";

    [MenuItem("Tools/GrimArchive/Haare UI 셋업 생성")]
    public static void SetupHaareUI()
    {
        if (!Directory.Exists(OutputFolder))
            Directory.CreateDirectory(OutputFolder);

        GameObject canvasPrefab = CreateCoreCanvasPrefab();
        GameObject panelPrefab = CreateDebugInfoPanelPrefab();
        GameObject fadePanelPrefab = CreateLoadingFadePanelPrefab();

        RegisterAddressable(canvasPrefab, CoreCanvasAddress);
        RegisterAddressable(panelPrefab, DebugPanelAddress);
        RegisterAddressable(fadePanelPrefab, LoadingFadePanelAddress);

        WireCompositionRoot(canvasPrefab);

        Debug.Log("[HaareUISetup] 완료: CoreCanvas / DebugInfoPanel / LoadingFadePanel 프리팹 생성, Addressable 등록, CompositionRoot 배선까지 마쳤습니다.");
    }

    private static GameObject CreateCoreCanvasPrefab()
    {
        var root = new GameObject("CoreCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        var safeArea = new GameObject("SafeArea", typeof(RectTransform));
        safeArea.transform.SetParent(root.transform, false);
        var safeRt = safeArea.GetComponent<RectTransform>();
        safeRt.anchorMin = Vector2.zero;
        safeRt.anchorMax = Vector2.one;
        safeRt.offsetMin = Vector2.zero;
        safeRt.offsetMax = Vector2.zero;

        var coreUIManager = root.AddComponent<CoreUIManager>();
        var so = new SerializedObject(coreUIManager);
        so.FindProperty("safePannelRect").objectReferenceValue = safeRt;
        so.ApplyModifiedPropertiesWithoutUndo();

        // FPS 카운터 (좌상단, 상시 표시) — CoreCanvas는 DontDestroyOnLoad라 씬 전환에도 유지됨.
        GameObject fpsTextGo = TMP_DefaultControls.CreateText(GetTMPResources());
        fpsTextGo.name = "FPSText";
        fpsTextGo.transform.SetParent(root.transform, false);
        var fpsTextRt = fpsTextGo.GetComponent<RectTransform>();
        fpsTextRt.anchorMin = new Vector2(0, 1);
        fpsTextRt.anchorMax = new Vector2(0, 1);
        fpsTextRt.pivot = new Vector2(0, 1);
        fpsTextRt.anchoredPosition = new Vector2(10, -10);
        fpsTextRt.sizeDelta = new Vector2(120, 30);
        var fpsTmp = fpsTextGo.GetComponent<TextMeshProUGUI>();
        fpsTmp.font = GetKoreanFont();
        fpsTmp.fontSize = 18;
        fpsTmp.color = Color.yellow;
        fpsTmp.text = "FPS";
        var fpsCustomText = fpsTextGo.AddComponent<CustomText>();

        var fpsLogger = fpsTextGo.AddComponent<FPSLogger>();
        var fpsSo = new SerializedObject(fpsLogger);
        fpsSo.FindProperty("fpsText").objectReferenceValue = fpsCustomText;
        fpsSo.ApplyModifiedPropertiesWithoutUndo();

        string path = OutputFolder + "/CoreCanvas.prefab";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static GameObject CreateDebugInfoPanelPrefab()
    {
        var tmpResources = GetTMPResources();

        var root = new GameObject("DebugInfoPanel", typeof(RectTransform));
        var rootRt = root.GetComponent<RectTransform>();
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.offsetMin = Vector2.zero;
        rootRt.offsetMax = Vector2.zero;

        // 좌하단 선택 유닛 정보 박스
        var infoBox = new GameObject("InfoBox", typeof(RectTransform), typeof(Image));
        infoBox.transform.SetParent(root.transform, false);
        var infoBoxRt = infoBox.GetComponent<RectTransform>();
        infoBoxRt.anchorMin = new Vector2(0, 0);
        infoBoxRt.anchorMax = new Vector2(0, 0);
        infoBoxRt.pivot = new Vector2(0, 0);
        infoBoxRt.anchoredPosition = new Vector2(10, 10);
        infoBoxRt.sizeDelta = new Vector2(240, 480);
        infoBox.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
        infoBox.AddComponent<CustomImage>();

        GameObject infoTextGo = TMP_DefaultControls.CreateText(tmpResources);
        infoTextGo.name = "InfoText";
        infoTextGo.transform.SetParent(infoBox.transform, false);
        var infoTextRt = infoTextGo.GetComponent<RectTransform>();
        infoTextRt.anchorMin = Vector2.zero;
        infoTextRt.anchorMax = Vector2.one;
        infoTextRt.offsetMin = new Vector2(8, 8);
        infoTextRt.offsetMax = new Vector2(-8, -8);
        var infoTmp = infoTextGo.GetComponent<TextMeshProUGUI>();
        infoTmp.font = GetKoreanFont();
        // 선택 유닛 정보 줄 수가 상황에 따라 달라지는데(인간/몬스터, 상태이상 유무 등) 고정 폰트 크기로는
        // 박스를 넘쳐서 화면 아래로 잘려나간다 — 오토사이징으로 항상 박스 안에 맞춰지도록 한다.
        infoTmp.enableAutoSizing = true;
        infoTmp.fontSizeMin = 8;
        infoTmp.fontSizeMax = 18;
        infoTmp.color = Color.white;
        infoTmp.alignment = TextAlignmentOptions.TopLeft;
        infoTmp.text = "";
        var infoCustomText = infoTextGo.AddComponent<CustomText>();

        // 맵 저장/불러오기 버튼 (우상단)
        GameObject saveGo = CreateCustomButton(tmpResources, "SaveButton", "Save");
        saveGo.transform.SetParent(root.transform, false);
        var saveRt = saveGo.GetComponent<RectTransform>();
        saveRt.anchorMin = new Vector2(1, 1);
        saveRt.anchorMax = new Vector2(1, 1);
        saveRt.pivot = new Vector2(1, 1);
        saveRt.anchoredPosition = new Vector2(-10, -10);
        saveRt.sizeDelta = new Vector2(90, 30);

        GameObject loadGo = CreateCustomButton(tmpResources, "LoadButton", "Load");
        loadGo.transform.SetParent(root.transform, false);
        var loadRt = loadGo.GetComponent<RectTransform>();
        loadRt.anchorMin = new Vector2(1, 1);
        loadRt.anchorMax = new Vector2(1, 1);
        loadRt.pivot = new Vector2(1, 1);
        loadRt.anchoredPosition = new Vector2(-10, -50);
        loadRt.sizeDelta = new Vector2(90, 30);

        var debugPanel = root.AddComponent<DebugInfoPanel>();
        var so = new SerializedObject(debugPanel);
        so.FindProperty("selectedUnitInfoText").objectReferenceValue = infoCustomText;
        so.FindProperty("saveButton").objectReferenceValue = saveGo.GetComponent<CustomButton>();
        so.FindProperty("loadButton").objectReferenceValue = loadGo.GetComponent<CustomButton>();
        so.ApplyModifiedPropertiesWithoutUndo();

        string path = OutputFolder + "/DebugInfoPanel.prefab";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    // GameUIPresenter(Assets/Script/UI/GameUIPresenter.cs)가 부팅 시 FadeIn()/FadeOut()으로 쓰는
    // 화면 전체를 덮는 검은 페이드 패널. Demo.UI.LoadingFadePanel 컴포넌트를 그대로 재사용하고
    // (Fade 로직은 그 클래스가 이미 갖고 있음) 비주얼(전체화면 검은 Image)만 이 프로젝트 걸로 만든다.
    private static GameObject CreateLoadingFadePanelPrefab()
    {
        var root = new GameObject("LoadingFadePanel", typeof(RectTransform), typeof(Image));
        var rootRt = root.GetComponent<RectTransform>();
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.offsetMin = Vector2.zero;
        rootRt.offsetMax = Vector2.zero;

        var image = root.GetComponent<Image>();
        image.color = Color.black;

        var customImage = root.AddComponent<CustomImage>();

        var fadePanel = root.AddComponent<Demo.UI.LoadingFadePanel>();
        var so = new SerializedObject(fadePanel);
        so.FindProperty("FadeImage").objectReferenceValue = customImage;
        so.ApplyModifiedPropertiesWithoutUndo();

        string path = OutputFolder + "/LoadingFadePanel.prefab";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static GameObject CreateCustomButton(TMP_DefaultControls.Resources tmpResources, string name, string label)
    {
        GameObject go = TMP_DefaultControls.CreateButton(tmpResources);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Button>());
        go.AddComponent<CustomImage>();
        go.AddComponent<CustomButton>();

        var textGo = go.transform.Find("Text (TMP)").gameObject;
        var buttonTmp = textGo.GetComponent<TextMeshProUGUI>();
        buttonTmp.font = GetKoreanFont();
        buttonTmp.text = label;
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
            Debug.LogError("[HaareUISetup] AddressableAssetSettings를 찾을 수 없습니다. Addressables 초기 설정을 먼저 확인하세요.");
            return;
        }

        var group = settings.FindGroup("Default Local Group") ?? settings.DefaultGroup;
        var entry = settings.CreateOrMoveEntry(guid, group);
        entry.address = address;

        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
    }

    private static void WireCompositionRoot(GameObject canvasPrefab)
    {
        Scene scene;
        bool closeAfter = false;

        var active = EditorSceneManager.GetActiveScene();
        if (active.path == ScenePath)
        {
            scene = active;
        }
        else
        {
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            closeAfter = true;
        }

        GameObject compositionRootGo = null;
        foreach (var rootGo in scene.GetRootGameObjects())
        {
            if (rootGo.name == "CompositionRoot")
            {
                compositionRootGo = rootGo;
                break;
            }
        }

        if (compositionRootGo == null)
        {
            Debug.LogError($"[HaareUISetup] {ScenePath}에서 'CompositionRoot' GameObject를 찾을 수 없습니다.");
            if (closeAfter) EditorSceneManager.CloseScene(scene, true);
            return;
        }

        var compRoot = compositionRootGo.GetComponent<GameCompositionRoot>();
        var coreUIManagerComponent = canvasPrefab.GetComponent<CoreUIManager>();

        var so = new SerializedObject(compRoot);
        so.FindProperty("_coreUIManagerPrefab").objectReferenceValue = coreUIManagerComponent;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        if (closeAfter)
            EditorSceneManager.CloseScene(scene, true);
    }

    private static Sprite GetBuiltinSprite(string path) => AssetDatabase.GetBuiltinExtraResource<Sprite>(path);

    private static TMP_FontAsset _koreanFont;
    private static TMP_FontAsset GetKoreanFont()
    {
        if (_koreanFont == null)
        {
            _koreanFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(KoreanFontPath);
            if (_koreanFont == null)
                Debug.LogError($"[HaareUISetup] 한글 폰트를 찾을 수 없습니다: {KoreanFontPath} (TMP 기본 폰트로 대체되어 한글이 깨질 수 있음)");
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
