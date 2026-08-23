// 스킬의 타겟팅을 서로 독립적인 두 축으로 나눈 정의(2026-08-23).
//
// 처음에는 SkillTargeting 하나에 4분류(SelfArea/Projectile/TargetArea/Support)를 담았는데, 앞의 셋은
// "판정을 어디에 만드는가"인 반면 Support만 "누구를 대상으로 하는가"여서 축이 섞여 있었다. 그러면
// "아군 좌표를 기준으로 터지는 광역 힐" 같은 조합을 표현할 방법이 없다. 두 축으로 나누면 그런 스킬도
// (TargetArea, Ally)라는 조합으로 그냥 나온다.

/// <summary>
/// 판정을 어디에 만드는가.
/// </summary>
public enum SkillOrigin
{
    /// <summary>시전자 자신을 기준으로 뻗는 히트박스. (Generic/Backstab/MultiHit/Curse/Heal/Shield)</summary>
    SelfArea,

    /// <summary>시전자에게서 발사되어 날아간다. 발동 조건은 SelfArea와 같다. (Projectile)</summary>
    Projectile,

    /// <summary>시전자 앞이 아니라 "대상이 서 있는 좌표"에 판정을 만든다. 전방 히트박스가 아니라
    /// 대상까지의 거리로 사거리를 따진다. (GroundAoE/Fireball)</summary>
    TargetArea,
}

/// <summary>
/// 누구를 대상으로 하는가.
/// </summary>
public enum SkillAffinity
{
    /// <summary>적. AI가 찾아준 가장 가까운 적을 그대로 대상으로 삼는다.</summary>
    Enemy,

    /// <summary>아군(또는 자신). 대상은 스킬이 SkillAction.ResolveTarget에서 스스로 찾는다.
    /// (Heal/Shield/PartyBuff)</summary>
    Ally,
}
