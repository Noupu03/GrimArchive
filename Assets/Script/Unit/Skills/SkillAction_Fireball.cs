/// <summary>
/// 파이어볼 — 시전자에게서 날아가는 투사체가 아니라, 지정한 좌표에 지연 착탄하는 "메테오"형 스킬이다.
/// 흐름은 부모 SkillAction_GroundAoE가 담당한다: 시전 순간 대상 좌표를 클로저에 캡처해 굳히고 위협
/// 타일을 띄운 뒤, baseDelayMs가 지나야 그 좌표에 범위 판정+마법 피해+착탄 이펙트(VFX_Fire)가 실행된다
/// — 좌표가 굳어 있어 대상이 이동하면 빗나갈 수 있다(회피 가능).
/// </summary>
public class SkillAction_Fireball : SkillAction_GroundAoE
{
    public SkillAction_Fireball(SkillData data) : base(data)
    {
        SkillName = string.IsNullOrEmpty(data.skillName) ? "파이어볼" : data.skillName;
    }
}
