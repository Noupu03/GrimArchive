using System;
using Haare.Util.Logger;

// "자원 소모 여부 확인 → 성공 시 설치, 실패 시 경고" 흐름을 한 곳으로 모은다. 실제 자원 소모 판정은
// 호출부마다 조금씩 달라 그대로 호출부 책임으로 남기고, 이 헬퍼는 결과(bool)를 받아 성공/실패
// 분기만 담당한다.
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
