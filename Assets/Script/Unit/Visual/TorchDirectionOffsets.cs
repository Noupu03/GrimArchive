using UnityEngine;

// 횃불 방향별 부착 위치 미세조정 — 벽 방향(위/오른쪽/아래/왼쪽)마다 스프라이트의 시각적 무게중심이
// 달라 타일 중앙에 그대로 놓으면 벽에서 뜨거나 파고든 것처럼 보인다. 값에 범위 제한을 두지 않아
// 타일 경계를 넘어가는 조정도 그대로 반영된다. FogOfWarSystem.SpawnTorchAt이 타일 중앙 월드 좌표에
// 이 오프셋을 더한다.
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
