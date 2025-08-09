using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Managers.UnitManagers;
using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.Models.Game.Items.Actions;
using AAEmu.Game.Models.StaticValues;
using MySql.Data.MySqlClient;
using AAEmu.Game.Models.Game.Skills.Templates;
using AAEmu.Game.Models;
using AAEmu.Game.Models.Game.Skills;
using AAEmu.Game.Models.Game.Units;

namespace AAEmu.Game.Models.Game.Char;

public partial class CharacterActability
{
    public Dictionary<uint, Actability> Actabilities { get; set; }

    public Character Owner { get; set; }

    public CharacterActability(Character owner)
    {
        Owner = owner;
        Actabilities = [];
    }

    /// <summary>
    /// Adds points to a specific ActAbility (life skill)
    /// </summary>
    /// <param name="id"></param>
    /// <param name="point"></param>
    /// <returns>The amount that was actually changed</returns>
    public int AddPoint(uint id, int point)
    {
        if (!Actabilities.TryGetValue(id, out var actability))
            return 0;
        var previousPoints = actability.Point;
        actability.Point += point;

        var template = CharacterManager.Instance.GetExpertLimit(actability.Step);
        if (actability.Point > template.UpLimit)
            actability.Point = template.UpLimit;
        return actability.Point - previousPoints;
    }

    public void Regrade(uint id, bool isUpgrade)
    {
        var actability = Actabilities[id];

        // TODO add validation to expert limit, if expert_limit = 0 -> infinity

        if (isUpgrade)
        {
            var template = CharacterManager.Instance.GetExpertLimit(actability.Step);
            if (template == null)
                return; // TODO ... send msg error?

            if (actability.Point < template.UpLimit)
                return; // TODO ... send msg error?

            actability.Step++;
        }
        else
        {
            var template = CharacterManager.Instance.GetExpertLimit(actability.Step - 1);
            if (template == null)
                return; // TODO ... send msg error?

            actability.Step--;
            actability.Point = template.UpLimit;
        }

        Owner.SendPacket(new SCExpertLimitModifiedPacket(isUpgrade, id, actability.Step));

        // Apply or remove rank-based passive buffs tied to this actability
        ApplyRankBuffsFor(id);
    }

    public void ExpandExpert()
    {
        var expand = CharacterManager.Instance.GetExpandExpertLimit(Owner.ExpandedExpert);
        if (expand == null)
            return; // TODO ... send msg error?

        if (expand.LifePoint > Owner.VocationPoint)
        {
            Owner.SendErrorMessage(ErrorMessageType.NotEnoughExpandItemAndMoney);
            return; // TODO ... send msg error?
        }

        if (expand.ItemId != 0 && expand.ItemCount != 0 && !Owner.Inventory.CheckItems(Items.SlotType.Inventory, expand.ItemId, expand.ItemCount))
        {
            Owner.SendErrorMessage(ErrorMessageType.NotEnoughExpandItem);
            return; // TODO ... send msg error?
        }

        if (expand.LifePoint > 0)
        {
            Owner.ChangeGamePoints(GamePointKind.Vocation, expand.LifePoint);
        }

        if (expand.ItemId != 0 && expand.ItemCount != 0)
        {
            Owner.Inventory.Bag.ConsumeItem(ItemTaskType.ExpandExpert, expand.ItemId, expand.ItemCount, null);
            /*
            var items = Owner.Inventory.RemoveItem(expand.ItemId, expand.ItemCount);

            var tasks = new List<ItemTask>();
            foreach (var (item, count) in items)
            {
                if (item.Count == 0)
                    tasks.Add(new ItemRemove(item));
                else
                    tasks.Add(new ItemCountUpdate(item, -count));
            }

            Owner.SendPacket(new SCItemTaskSuccessPacket(ItemTaskType.ExpandExpert, tasks, new List<ulong>()));
            */
        }

        Owner.ExpandedExpert = expand.ExpandCount;
        Owner.SendPacket(new SCExpertExpandedPacket(Owner.ExpandedExpert));
    }

    public void Send()
    {
        Owner.SendPacket(new SCActabilityPacket(true, Actabilities.Values.ToArray()));
    }

    public void Load(MySqlConnection connection)
    {
        using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT * FROM actabilities WHERE `owner` = @owner";
            command.Parameters.AddWithValue("@owner", Owner.Id);
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    var id = reader.GetUInt32("id");
                    var template = CharacterManager.Instance.GetActability(id);

                    var actability = new Actability(template)
                    {
                        Id = id,
                        Point = reader.GetInt32("point"),
                        Step = reader.GetByte("step")
                    };
                    Actabilities.Add(actability.Id, actability);
                }
            }
        }

        // After loading, ensure rank-based passive buffs are applied
        ApplyAllRankBuffs();
    }

    public void Save(MySqlConnection connection, MySqlTransaction transaction)
    {
        foreach (var actability in Actabilities.Values)
        {
            using (var command = connection.CreateCommand())
            {
                command.Connection = connection;
                command.Transaction = transaction;

                command.CommandText = "REPLACE INTO actabilities(`id`,`point`,`step`,`owner`) VALUES (@id, @point, @step, @owner)";
                command.Parameters.AddWithValue("@id", (byte)actability.Id);
                command.Parameters.AddWithValue("@point", actability.Point);
                command.Parameters.AddWithValue("@step", actability.Step);
                command.Parameters.AddWithValue("@owner", Owner.Id);
                command.ExecuteNonQuery();
            }
        }
    }
}

// Helpers to apply rank-based passive buffs by Actability rank
public partial class CharacterActability
{
    private void ApplyAllRankBuffs()
    {
        if (AppConfiguration.Instance?.ActabilityRankBuffs == null || AppConfiguration.Instance.ActabilityRankBuffs.Count == 0)
            return;

        foreach (var rule in AppConfiguration.Instance.ActabilityRankBuffs)
        {
            ApplyRule(rule);
        }
    }

    private void ApplyRankBuffsFor(uint actabilityId)
    {
        if (AppConfiguration.Instance?.ActabilityRankBuffs == null || AppConfiguration.Instance.ActabilityRankBuffs.Count == 0)
            return;

        foreach (var rule in AppConfiguration.Instance.ActabilityRankBuffs)
        {
            if (rule.ActabilityId == actabilityId)
                ApplyRule(rule);
        }
    }

    private void ApplyRule(AppConfiguration.ActabilityRankBuffRule rule)
    {
        if (!Actabilities.TryGetValue(rule.ActabilityId, out var act)
            && !Actabilities.TryGetValue((byte)rule.ActabilityId, out act))
            return;

        var hasBuff = Owner.Buffs.CheckBuff(rule.BuffId);
        if (act.Step >= rule.MinStep)
        {
            if (!hasBuff)
            {
                var buffTemplate = SkillManager.Instance.GetBuffTemplate(rule.BuffId);
                if (buffTemplate != null)
                {
                    var newEffect = new Buff(Owner, Owner, new SkillCasterUnit(), buffTemplate, null, DateTime.UtcNow)
                    {
                        Passive = true
                    };
                    Owner.Buffs.AddBuff(newEffect);
                }
            }
        }
        else
        {
            if (hasBuff)
            {
                Owner.Buffs.RemoveBuff(rule.BuffId);
            }
        }
    }
}
