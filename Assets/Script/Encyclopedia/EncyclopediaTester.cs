using UnityEngine;
using UnityEngine.InputSystem;
using Game.Encyclopedia;

public class EncyclopediaTester : MonoBehaviour
{
    [Header("테스트할 도감 ID 목록 (예: Monster_001, Monster_002)")]
    public string[] entryIdsToUnlock = { "Monster_001", "Monster_002" };

    void Update()
    {
        if (EncyclopediaManager.Instance == null) return;
        if (Keyboard.current == null) return;

        // 키보드 숫자 1번을 누르면 첫 번째 도감 해제 테스트
        if (Keyboard.current.digit1Key.wasPressedThisFrame)
        {
            if (entryIdsToUnlock.Length > 0)
            {
                bool success = EncyclopediaManager.Instance.UnlockEntry(entryIdsToUnlock[0]);
                Debug.Log($"[테스트] 도감 해제 시도 '{entryIdsToUnlock[0]}': 결과 = {success}");
            }
        }

        // 키보드 숫자 2번을 누르면 두 번째 도감 해제 테스트
        if (Keyboard.current.digit2Key.wasPressedThisFrame)
        {
            if (entryIdsToUnlock.Length > 1)
            {
                bool success = EncyclopediaManager.Instance.UnlockEntry(entryIdsToUnlock[1]);
                Debug.Log($"[테스트] 도감 해제 시도 '{entryIdsToUnlock[1]}': 결과 = {success}");
            }
        }
    }
}
