namespace Content.Shared._CorvaxNext.Skills;

public sealed class SkillsSystem : EntitySystem
{
    public bool HasSkill(EntityUid entity, Skills skill, SkillsComponent? component = null)
    {
        if (!Resolve(entity, ref component))
            return false;

        return component.Skills.Contains(skill);
    }
}
