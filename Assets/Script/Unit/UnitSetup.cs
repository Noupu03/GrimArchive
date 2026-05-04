using UnityEngine;
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
            Debug.Log("Unit Environment Setup Complete: UnitManager created.");
        }
        else
        {
            Debug.Log("Unit Environment is already set up!");
        }
    }

    [MenuItem("Tools/Setup UI Environment")]
    public static void SetupUI()
    {
        if (FindObjectOfType<UIManager>() == null)
        {
            GameObject managerObj = new GameObject("UIManager");
            managerObj.AddComponent<UIManager>();
            Debug.Log("UI Environment Setup Complete: UIManager created.");
        }
        else
        {
            Debug.Log("UI Environment is already set up!");
        }
    }

    [MenuItem("Tools/Setup Camera Controller")]
    public static void SetupCamera()
    {
        if (Camera.main != null && Camera.main.gameObject.GetComponent<CameraController>() == null)
        {
            Camera.main.gameObject.AddComponent<CameraController>();
            Debug.Log("Camera Controller Setup Complete: Added to Main Camera.");
        }
        else if (Camera.main == null)
        {
            Debug.LogWarning("Main Camera not found. Please tag a camera as MainCamera.");
        }
        else
        {
            Debug.Log("Camera Controller is already set up!");
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
            Debug.Log("Unit Environment Setup Complete: UnitManager created.");
        }

        if (FindObjectOfType<UIManager>() == null)
        {
            GameObject uiObj = new GameObject("UIManager");
            uiObj.AddComponent<UIManager>();
            Debug.Log("UI Environment Setup Complete.");
        }

        if (Camera.main != null && Camera.main.gameObject.GetComponent<CameraController>() == null)
        {
            Camera.main.gameObject.AddComponent<CameraController>();
            Debug.Log("Camera Controller Setup Complete.");
        }
    }
}
