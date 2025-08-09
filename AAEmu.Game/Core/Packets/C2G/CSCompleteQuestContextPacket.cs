using AAEmu.Commons.Network;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Network.Game;
using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.Models.Game.Quests;
using AAEmu.Game.Models.Game.Quests.Acts;
using AAEmu.Game.Models.Game.Quests.Static;
using System.Linq;

namespace AAEmu.Game.Core.Packets.C2G;

public class CSCompleteQuestContextPacket : GamePacket
{
    private uint _questContextId;
    private uint _npcObjId;
    private uint _doodadObjId;
    private int _selected;

    public CSCompleteQuestContextPacket() : base(CSOffsets.CSCompleteQuestContextPacket, 1)
    {
    }

    public override void Read(PacketStream stream)
    {
        _questContextId = stream.ReadUInt32();
        _npcObjId = stream.ReadBc();
        _doodadObjId = stream.ReadBc();
        _selected = stream.ReadInt32();

        // Normalize selection to 1-based (client may send 0). Optionally bound to available selective rewards.
        if (_selected <= 0)
            _selected = 1;

        // Send an immediate ack/update to keep UI responsive, if quest is active.
        if (Connection?.ActiveChar?.Quests?.ActiveQuests?.TryGetValue(_questContextId, out var activeQuest) == true)
        {
            // If selection is out of range and we can detect it, clamp to a sane value
            if (activeQuest.QuestSteps.TryGetValue(QuestComponentKind.Reward, out var rewardStep))
            {
                var maxSel = rewardStep.Components.Values
                    .SelectMany(c => c.Acts)
                    .Count(a => a.Template is QuestActSupplySelectiveItem);
                if (maxSel > 0 && _selected > maxSel)
                    _selected = maxSel;
            }

            Connection.ActiveChar.SendPacket(new SCQuestContextUpdatedPacket(activeQuest, activeQuest.ComponentId));
        }

        // Hand off the potentially heavy work to a background task so the network thread stays responsive.
        System.Threading.Tasks.Task.Run(() =>
            QuestManager.Instance.DoReportEvents(Connection.ActiveChar, _questContextId, _npcObjId, _doodadObjId, _selected)
        );
    }

}
