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
            Debug.Log("Unit Environment Setup Complete: UnitManager created with GameSession and UnitGenerate.");
        }
        else
        {
            Debug.Log("Unit Environment is already set up!");
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
            Debug.Log("Unit Environment Setup Complete.");
        }
    }
}
