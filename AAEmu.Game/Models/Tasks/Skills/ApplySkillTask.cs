using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Models.Game.Units;

namespace AAEmu.Game.Models.Tasks.Skills;

public class ApplySkillTask : Task
{
    private readonly Skill _skill;
    private readonly BaseUnit _caster;
    private readonly SkillCaster _casterCaster;
    private readonly BaseUnit _target;
    private readonly SkillCastTarget _targetCaster;
    private readonly SkillObject _skillObject;

    public ApplySkillTask(Skill skill, BaseUnit caster, SkillCaster casterCaster, BaseUnit target, SkillCastTarget targetCaster, SkillObject skillObject)
    {
        _skill = skill;
        _caster = caster;
        _casterCaster = casterCaster;
        _target = target;
        _targetCaster = targetCaster;
        _skillObject = skillObject;
    }

    public override void Execute()
    {
        try
        {
            NLog.LogManager.GetCurrentClassLogger().Debug(
                "ApplySkillTask start: skill={0}, tlId={1}, caster={2}, target={3}",
                _skill?.Template?.Id, _skill?.TlId, _caster?.ObjId, _targetCaster?.ObjId);
            _skill.ApplyEffects(_caster, _casterCaster, _target, _targetCaster, _skillObject);
        }
        catch (Exception e)
        {
            // Ensure the client is not left hanging if an effect application throws
            NLog.LogManager.GetCurrentClassLogger().Error("ApplySkillTask exception for skill {0}: {1}\n{2}", _skill?.Template?.Id, e.Message, e.StackTrace);
        }
        finally
        {
            _skill.EndSkill(_caster);
        }
    }
}
