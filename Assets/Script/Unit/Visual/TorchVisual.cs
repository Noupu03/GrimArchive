using UnityEngine;
#if UNITY_2022_2_OR_NEWER
using UnityEngine.U2D.Animation;
#endif

// 횃불 방향별 스프라이트 적용(2026-08-21) — 원래 FogOfWarSystem(Session 계층, "순수 시각 오버레이라
// 이동/시야/AI 판정 등 어떤 게임플레이 로직도 건드리지 않는다"고 스스로 선언한 클래스)이 직접
// SpriteResolver를 조작하고 있었다. 유닛의 방향별 스프라이트 전환(UnitGenerate.UpdateSpriteResolver)이
// 이미 Visual 계층에 있는 것과 같은 이유로, 횃불도 스프라이트 적용 책임을 이 Visual 계층으로 옮겼다 —
// FogOfWarSystem.SpawnTorchAt은 이제 ApplyTorchVisual 한 번만 호출한다.
public static class TorchVisual
{
    // 벽 방향 → 스프라이트 라벨/반전. new_torch.png에는 Up/Right/Down 3종만 있고, Left는 Right를
    // 좌우 반전해서 재사용한다(사용자 요청).
    public static void GetSpriteLabel(TorchWallSide side, out string label, out bool flipX)
    {
        flipX = false;
        switch (side)
        {
            case TorchWallSide.Top:    label = "Up";    break;
            case TorchWallSide.Bottom: label = "Down";  break;
            case TorchWallSide.Right:  label = "Right"; break;
            default:                   label = "Right"; flipX = true; break; // Left
        }
    }

    // Torch.prefab에 미리 붙여둔 SpriteLibrary/SpriteResolver(자식 "Visual")와 TorchDirectionOffsets
    // (루트)를 찾아 방향에 맞는 스프라이트 + 위치 보정을 적용한다. UnitGenerate.UpdateSpriteResolver와
    // 동일한 SpriteResolver 적용 절차는 UnitSpriteManager.ApplySpriteResolverLabel로 공유한다.
    public static void ApplyTorchVisual(GameObject go, TorchWallSide side)
    {
#if UNITY_2022_2_OR_NEWER
        var resolver = go.GetComponentInChildren<SpriteResolver>();
        if (resolver == null) return;

        GetSpriteLabel(side, out string label, out bool flipX);
        UnitSpriteManager.ApplySpriteResolverLabel(resolver, "A", label, flipX);

        // 방향별 위치 보정(2026-08-21, 사용자 요청 "torch 프리팹에서 상하 좌우 별 위치 pivot 조정할
        // 수 있게 해줘. (타일 초과 가능)" → "스프라이트 오프셋만 조절되고, 생성 위치 자체는 그대로인거로
        // 처리해줘.(빛 때문에 그럼)") — resolver가 붙은 "Visual" 자식의 로컬 위치만 옮긴다. 루트(빛의
        // 부모)는 절대 건드리지 않는다.
        var directionOffsets = go.GetComponent<TorchDirectionOffsets>();
        if (directionOffsets != null)
        {
            Vector2 dirOffset = directionOffsets.GetOffset(side);
            resolver.transform.localPosition = new Vector3(dirOffset.x, dirOffset.y, 0f);
        }
#else
        Haare.Util.Logger.LogHelper.Error(Haare.Util.Logger.LogHelper.GAME, "SpriteResolver는 Unity 2022.2 이상에서 지원됩니다.");
#endif
    }
}
