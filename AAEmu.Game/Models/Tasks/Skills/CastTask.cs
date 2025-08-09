using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Models.Game.Units;

namespace AAEmu.Game.Models.Tasks.Skills;

public class CastTask : SkillTask
{
    private readonly BaseUnit _caster;
    private readonly SkillCaster _casterCaster;
    private readonly BaseUnit _target;
    private readonly SkillCastTarget _targetCaster;
    private readonly SkillObject _skillObject;

    public CastTask(Skill skill, BaseUnit caster, SkillCaster casterCaster, BaseUnit target, SkillCastTarget targetCaster, SkillObject skillObject) : base(skill)
    {
        _caster = caster;
        _casterCaster = casterCaster;
        _target = target;
        _targetCaster = targetCaster;
        _skillObject = skillObject;
    }

    public override void Execute()
    {
        if (Skill.Cancelled)
            return;
        try
        {
            NLog.LogManager.GetCurrentClassLogger().Debug(
                "CastTask start: skill={0}, tlId={1}, caster={2}, target={3}",
                Skill?.Template?.Id, Skill?.TlId, _caster?.ObjId, _targetCaster?.ObjId);
            Skill.Cast(_caster, _casterCaster, _target, _targetCaster, _skillObject);
        }
        catch (Exception e)
        {
            // Do not leave the cast hanging if something goes wrong
            NLog.LogManager.GetCurrentClassLogger().Error("CastTask exception for skill {0}: {1}\n{2}", Skill?.Template?.Id, e.Message, e.StackTrace);
            try { Skill.EndSkill(_caster); } catch { /* best effort */ }
        }
    }
}
