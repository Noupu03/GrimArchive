using UnityEngine;
using Game.Encyclopedia;

// 입력 정리(2026-08-21, 사용자 요청 "wasd, 마우스 휠, 스페이스바, 마우스 좌클릭 우클릭, 0123
// 속도조절만 남기고 전부 없애줘") — 이 테스터가 쓰던 숫자 1/2번 키가 InputManager의 게임 속도 단축키
// (0~3)와 같은 물리 키를 다른 용도로 겹쳐 쓰고 있었다. 키 입력 트리거를 없애고 코드에서 직접 호출하는
// 수동 테스트용 메서드만 남긴다.
public class EncyclopediaTester : MonoBehaviour
{
    [Header("테스트할 도감 ID 목록 (예: Monster_001, Monster_002)")]
    public string[] entryIdsToUnlock = { "Monster_001", "Monster_002" };

    public void UnlockTestEntry(int index)
    {
        if (EncyclopediaManager.Instance == null) return;
        if (index < 0 || index >= entryIdsToUnlock.Length) return;

        bool success = EncyclopediaManager.Instance.UnlockEntry(entryIdsToUnlock[index]);
        Debug.Log($"[테스트] 도감 해제 시도 '{entryIdsToUnlock[index]}': 결과 = {success}");
    }
}
