using System.Collections.Generic;
using UnityEngine;

public interface IMovementAlgorithm
{
    bool TryGetNextStep(Unit unit, Vector2Int targetPos, out Dir nextDir);

    // 명령 경로 시각화용(2026-08-24 사용자 요청 "유닛이 명령받은 지점과, 명령 경로가 뜨도록... 길찾기
    // 알고리즘에 따른 변경도 같이 실시간 반영") — TryGetNextStep이 매 호출마다 이미 계산해 캐시해 둔
    // 내부 경로를 그대로 재사용한다(새로 계산하지 않음 — 실제 이동 판단에 쓰는 것과 항상 동일한 경로를
    // 보여주기 위함, 매 프레임 읽어도 비용이 사실상 없다).
    bool TryGetCachedDestination(out Vector2Int destination);
    List<Vector2Int> BuildCachedPathPreview(Unit unit, int maxSteps);
    void ClearCache();
}
