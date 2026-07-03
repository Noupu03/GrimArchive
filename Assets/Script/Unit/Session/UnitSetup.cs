using UnityEngine;
using Haare.Util.Logger;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class UnitSetup : MonoBehaviour
{
#if UNITY_EDITOR
    [MenuItem("Tools/Setup Unit Environment")]
    public static void SetupManagers()
    {
        if (FindObjectOfType<GameSession>() == null)
        {
            GameObject managerObj = new GameObject("UnitManager");
            managerObj.AddComponent<GameSession>();
            managerObj.AddComponent<UnitGenerate>();
            managerObj.AddComponent<InputManager>();
            managerObj.AddComponent<PartyController>();
            managerObj.AddComponent<ArtifactManager>();
            LogHelper.Log(LogHelper.GAME, "Unit Environment Setup Complete: UnitManager created.");
        }
        else
        {
            LogHelper.Log(LogHelper.GAME, "Unit Environment is already set up!");
        }

        // UnitSpriteManager 설정
        if (FindObjectOfType<UnitSpriteManager>() == null)
        {
            GameObject spriteManagerObj = new GameObject("UnitSpriteManager");
            spriteManagerObj.AddComponent<UnitSpriteManager>();
            LogHelper.Log(LogHelper.GAME, "Unit Sprite Manager Setup Complete.");
        }
    }

    [MenuItem("Tools/Setup UI Environment")]
    public static void SetupUI()
    {
        if (FindObjectOfType<UIManager>() == null)
        {
            GameObject managerObj = new GameObject("UIManager");
            managerObj.AddComponent<UIManager>();
            LogHelper.Log(LogHelper.GAME, "UI Environment Setup Complete: UIManager created.");
        }
        else
        {
            LogHelper.Log(LogHelper.GAME, "UI Environment is already set up!");
        }
    }

    [MenuItem("Tools/Setup Camera Controller")]
    public static void SetupCamera()
    {
        if (Camera.main != null && Camera.main.gameObject.GetComponent<CameraController>() == null)
        {
            Camera.main.gameObject.AddComponent<CameraController>();
            LogHelper.Log(LogHelper.GAME, "Camera Controller Setup Complete: Added to Main Camera.");
        }
        else if (Camera.main == null)
        {
            LogHelper.Warning(LogHelper.GAME, "Main Camera not found. Please tag a camera as MainCamera.");
        }
        else
        {
            LogHelper.Log(LogHelper.GAME, "Camera Controller is already set up!");
        }
    }
#endif

    [ContextMenu("Setup Unit Manager Component")]
    public void SetupComponent()
    {
        if (FindObjectOfType<GameSession>() == null)
        {
            GameObject managerObj = new GameObject("UnitManager");
            managerObj.AddComponent<GameSession>();
            managerObj.AddComponent<UnitGenerate>();
            managerObj.AddComponent<InputManager>();
            managerObj.AddComponent<PartyController>();
            managerObj.AddComponent<ArtifactManager>();
            LogHelper.Log(LogHelper.GAME, "Unit Environment Setup Complete: UnitManager created.");
        }

        if (FindObjectOfType<UIManager>() == null)
        {
            GameObject uiObj = new GameObject("UIManager");
            uiObj.AddComponent<UIManager>();
            LogHelper.Log(LogHelper.GAME, "UI Environment Setup Complete.");
        }

        if (Camera.main != null && Camera.main.gameObject.GetComponent<CameraController>() == null)
        {
            Camera.main.gameObject.AddComponent<CameraController>();
            LogHelper.Log(LogHelper.GAME, "Camera Controller Setup Complete.");
        }

        if (FindObjectOfType<UnitSpriteManager>() == null)
        {
            GameObject spriteManagerObj = new GameObject("UnitSpriteManager");
            spriteManagerObj.AddComponent<UnitSpriteManager>();
            LogHelper.Log(LogHelper.GAME, "Unit Sprite Manager Setup Complete.");
        }
    }
}
