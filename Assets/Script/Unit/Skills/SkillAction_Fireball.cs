public class SkillAction_Fireball : SkillAction_GroundAoE
{
    public SkillAction_Fireball(SkillData data) : base(data)
    {
        SkillName = string.IsNullOrEmpty(data.skillName) ? "파이어볼" : data.skillName;
    }
}
