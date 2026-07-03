using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro; 
using Haare.Client.UI;

public class FrameworkMenuItems
{
    [MenuItem("GameObject/HaareFramework UI/Custom Image")]
    private static void CreateCustomImage(MenuCommand menuCommand)
    {
        var go = new GameObject("Custom Image");
        go.AddComponent<Image>(); 
        go.AddComponent<CustomImage>();
        SetupAndRegister(go, menuCommand);
    }

    [MenuItem("GameObject/HaareFramework UI/Custom Text (TMP)")]
    private static void CreateCustomText(MenuCommand menuCommand)
    {
        var go = new GameObject("Custom Text (TMP)");
        go.AddComponent<TextMeshProUGUI>();
        go.AddComponent<CustomText>();
        SetupAndRegister(go, menuCommand);
    }

    [MenuItem("GameObject/HaareFramework UI/Custom Button")]
    private static void CreateCustomButton(MenuCommand menuCommand)
    {
        var go = new GameObject("Custom Button");
        go.AddComponent<Image>(); 
        go.AddComponent<CustomButton>();
        SetupAndRegister(go, menuCommand);
    }

    [MenuItem("GameObject/HaareFramework UI/Custom Slider")]
    private static void CreateCustomSlider(MenuCommand menuCommand)
    {
        var go = new GameObject("Custom Slider");
        
        var slider = go.AddComponent<Slider>();
        go.AddComponent<CustomSlider>();
        
        SetupAndRegister(go, menuCommand);
    }

    // --- 유효성 검사(Validation) 메서드 ---
    // 원본 저장소 자체에 "NovaFramework"로 오타나 있어서(다른 프로젝트에서 복붙된 흔적으로 추정)
    // 위 생성 메뉴들과 경로가 안 맞아 검증이 전혀 동작하지 않던 것을 수정함.
    [MenuItem("GameObject/HaareFramework UI/Custom Image", true)]
    [MenuItem("GameObject/HaareFramework UI/Custom Text (TMP)", true)]
    [MenuItem("GameObject/HaareFramework UI/Custom Button", true)]
    [MenuItem("GameObject/HaareFramework UI/Custom Slider", true)]
    private static bool ValidateUIElementCreation()
    {
        return Selection.activeGameObject != null && Selection.activeGameObject.GetComponentInParent<Canvas>();
    }

    // --- 도우미(Helper) 메서드 ---
    // 코드 중복을 줄이기 위한 함수입니다.
    
    private static void SetupAndRegister(GameObject go, MenuCommand menuCommand)
    {
        GameObjectUtility.SetParentAndAlign(go, menuCommand.context as GameObject);
        Undo.RegisterCreatedObjectUndo(go, "Create " + go.name);
        Selection.activeObject = go;
    }
}