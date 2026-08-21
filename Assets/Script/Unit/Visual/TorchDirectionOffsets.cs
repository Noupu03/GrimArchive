using UnityEngine;

// 횃불 방향별 부착 위치 미세조정(2026-08-21, 사용자 요청 "torch 프리팹에서 상하 좌우 별 위치 pivot
// 조정할 수 있게 해줘. (타일 초과 가능)") — 벽 방향(위/오른쪽/아래/왼쪽)마다 new_torch.png 스프라이트의
// 시각적 무게중심(브라켓이 벽에 붙는 지점)이 달라서, 타일 중앙에 그대로 놓으면 방향에 따라 벽에서
// 살짝 뜨거나 파고든 것처럼 보일 수 있다. Inspector에서 방향별로 직접 오프셋을 조정할 수 있게 값에
// 범위 제한을 두지 않았다 — 타일 경계를 넘어가는 조정도 그대로 반영된다(요청 그대로 "타일 초과 가능").
// FogOfWarSystem.SpawnTorchAt이 타일 중앙 월드 좌표에 이 오프셋을 그대로 더한다.
public class TorchDirectionOffsets : MonoBehaviour
{
    [Tooltip("위쪽 벽(Up) 횃불의 타일 중앙 대비 위치 보정값")]
    public Vector2 upOffset;

    [Tooltip("오른쪽 벽(Right) 횃불의 타일 중앙 대비 위치 보정값")]
    public Vector2 rightOffset;

    [Tooltip("아래쪽 벽(Down) 횃불의 타일 중앙 대비 위치 보정값")]
    public Vector2 downOffset;

    [Tooltip("왼쪽 벽(Left, Right 스프라이트를 좌우 반전해서 사용) 횃불의 타일 중앙 대비 위치 보정값")]
    public Vector2 leftOffset;

    public Vector2 GetOffset(TorchWallSide side) => side switch
    {
        TorchWallSide.Top    => upOffset,
        TorchWallSide.Right  => rightOffset,
        TorchWallSide.Bottom => downOffset,
        _                    => leftOffset,
    };
}
