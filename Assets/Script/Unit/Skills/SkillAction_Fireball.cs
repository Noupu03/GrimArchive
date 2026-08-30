/// <summary>
/// 파이어볼 — 시전자에게서 날아가는 투사체가 아니라, 지정한 좌표에 지연 착탄하는 "메테오"형 스킬이다
/// (좌표 선택 후 시간이 지나 떨어지며, 시전 즉시 판정+피해가 나지 않는다).
///
/// 흐름은 부모 SkillAction_GroundAoE가 그대로 담당한다:
///   1) 시전 순간 대상 중심 좌표를 확정하고 그 자리에 3x3(explosionRadius 1.5) 위협 타일을 띄운다.
///   2) baseDelayMs 동안 시전 대기 — 이 사이 적은 예고 범위 밖으로 걸어나가 회피할 수 있다.
///   3) 대기가 끝나면 1)에서 굳힌 좌표에 범위 판정 + 마법 피해 + 착탄 이펙트(VFX_Fire).
/// 좌표는 1)에서 클로저에 캡처되므로 대상이 이동해도 따라가지 않는다(빗나갈 수 있다).
/// </summary>
public class SkillAction_Fireball : SkillAction_GroundAoE
{
    public SkillAction_Fireball(SkillData data) : base(data)
    {
        SkillName = string.IsNullOrEmpty(data.skillName) ? "파이어볼" : data.skillName;
    }
}
