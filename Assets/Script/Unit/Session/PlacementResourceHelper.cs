using System;
using Haare.Util.Logger;

// "자원 소모 여부 확인 → 성공 시 설치, 실패 시 경고" 흐름(2026-08-25 리팩토링 — BuildPlacementController/
// ObjectPlacementController가 유닛 건물/자원 건물/함정/문 재설치 4곳에서 각자 반복 구현하고 있었다)을
// 한 곳으로 모은다. 실제 자원 소모 판정(TryConsumeResource 호출과 null 체크 여부)은 호출부마다 조금씩
// 달라서(BuildPlacementController는 null 체크가 없고 ObjectPlacementController는 있음) 그대로
// 호출부 책임으로 남기고, 이 헬퍼는 그 결과(bool)를 받아 성공/실패 분기만 담당한다.
public static class PlacementResourceHelper
{
    public static void OnConsumeResult(bool consumed, Action onSuccess, string insufficientMessage)
    {
        if (consumed)
        {
            onSuccess();
        }
        else
        {
            LogHelper.Warning(LogHelper.GAME, insufficientMessage);
        }
    }
}
