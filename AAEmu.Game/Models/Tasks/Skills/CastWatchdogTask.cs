using System;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Models.Game.Units;
using NLog;

namespace AAEmu.Game.Models.Tasks.Skills;

public class CastWatchdogTask : Task
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private readonly Skill _skill;
    private readonly BaseUnit _caster;
    private readonly SkillCaster _casterCaster;
    private readonly BaseUnit _target;
    private readonly SkillCastTarget _targetCaster;
    private readonly SkillObject _skillObject;
    private readonly ushort _tlId;

    public CastWatchdogTask(Skill skill, BaseUnit caster, SkillCaster casterCaster, BaseUnit target, SkillCastTarget targetCaster, SkillObject skillObject, ushort tlId)
    {
        _skill = skill;
        _caster = caster;
        _casterCaster = casterCaster;
        _target = target;
        _targetCaster = targetCaster;
        _skillObject = skillObject;
        _tlId = tlId;
    }

    public override void Execute()
    {
        try
        {
            if (_skill == null || _caster is not Unit unit)
                return;

            // If the skill was already cancelled, don't do anything
            if (_skill.Cancelled)
            {
                Logger.Debug("CastWatchdog skip: skill={0}, tlId={1} already cancelled", _skill.Template?.Id, _tlId);
                return;
            }

            // If the scheduled cast already ran, the unit's SkillTask will be null (cleared in Skill.Cast)
            // Only intervene if the original cast task appears to still be pending for this TlId
            if (unit.SkillTask is CastTask ct && ct.TlIdSnapshot == _tlId)
            {
                Logger.Warn("CastWatchdog firing overdue cast: skill={0}, tlId={1}, caster={2}", _skill.Template?.Id, _tlId, unit.ObjId);
                // Proactively execute the cast now
                _skill.Cast(_caster, _casterCaster, _target, _targetCaster, _skillObject);
            }
            else
            {
                Logger.Debug("CastWatchdog no-op: tlId={0} not pending (maybe already executed)", _tlId);
            }
        }
        catch (Exception e)
        {
            Logger.Error("CastWatchdog exception for skill {0}: {1}\n{2}", _skill?.Template?.Id, e.Message, e.StackTrace);
            try { _skill?.EndSkill(_caster); } catch { /* best effort */ }
        }
    }
}
