using AAEmu.Game.Core.Managers;
using AAEmu.Game.Models.Game.Char;
using AAEmu.Game.Models.Game.DoodadObj.Templates;
using AAEmu.Game.Models.Game.Units;
using AAEmu.Game.Models.Tasks.Doodads;
using AAEmu.Game.Models.Game.Skills.Static;
using System.Linq;

namespace AAEmu.Game.Models.Game.DoodadObj.Funcs;

public class DoodadFuncTimer : DoodadPhaseFuncTemplate
{
    public int Delay { get; set; }
    public int NextPhase { get; set; }
    public bool KeepRequester { get; set; }
    public bool ShowTip { get; set; }
    public bool ShowEndTime { get; set; }
    public string Tip { get; set; }

    public override bool Use(BaseUnit caster, Doodad owner)
    {
        if (NextPhase > 0)
        {
            if (caster is Character)
                Logger.Debug("DoodadFuncTimer: TemplateId {0},  Delay {1}, NextPhase {2}, KeepRequester {3}, ShowTip {4}, ShowEndTime {5}, Tip {6}", owner.TemplateId, Delay, NextPhase, KeepRequester, ShowTip, ShowEndTime, Tip);
            else
                Logger.Trace("DoodadFuncTimer: TemplateId {0},  Delay {1}, NextPhase {2}, KeepRequester {3}, ShowTip {4}, ShowEndTime {5}, Tip {6}", owner.TemplateId, Delay, NextPhase, KeepRequester, ShowTip, ShowEndTime, Tip);

            double customDelay = Delay;

            // Apply production-time reductions from active buffs
            // Uses SkillModifiers by tag 1157 ("Decrease production time") with SkillAttribute.CastTime
            /*try
            {
                var mods = caster.SkillModifiersCache?.GetModifiersForTagIdWithAttribute(1157, SkillAttribute.CastTime);
                if (mods != null && mods.Count > 0)
                {
                    // Apply in the same order logic as SkillModifiers.ApplyModifiers
                    foreach (var modifier in mods.OrderBy(m => m.UnitModifierType))
                    {
                        switch (modifier.UnitModifierType)
                        {
                            case UnitModifierType.Percent:
                                customDelay += (customDelay * (modifier.Value / 100.0f));
                                break;
                            case UnitModifierType.Value:
                                customDelay += modifier.Value;
                                break;
                        }
                    }
                }
            }
            catch
            {
                // ignore and fall back to base delay
            }*/
            var timeLeft = customDelay;

            if (owner.OverridePhaseTime > DateTime.MinValue)
            {
                // Reset the override
                owner.PhaseTime = owner.OverridePhaseTime;
                owner.OverridePhaseTime = DateTime.MinValue;

                var timeSincePhaseStart = DateTime.UtcNow - owner.PhaseTime;
                timeLeft = customDelay - timeSincePhaseStart.TotalMilliseconds;
            }

            if (timeLeft < 1)
                timeLeft = 1;

            owner.GrowthTime = DateTime.UtcNow.AddMilliseconds(timeLeft);

            // Отменяем текущую задачу, если она существует
            // Cancel the current task if it exists
            if (owner.FuncTask != null)
            {
                try
                {
                    TaskManager.Instance.Cancel(owner.FuncTask);
                }
                catch (Exception ex)
                {
                    Logger.Error("Failed to cancel existing FuncTask: {0}", ex.Message);
                }
            }

            // Создаем и назначаем новую задачу
            // Create and assign a new task
            owner.FuncTask = new DoodadFuncTimerTask(caster, owner, 0, NextPhase);
            TaskManager.Instance.Schedule(owner.FuncTask, TimeSpan.FromMilliseconds(timeLeft));
        }

        // никогда не прерываем последовательность фазовых функций
        // we never interrupt the sequence of phase functions
        return false;
    }
}
