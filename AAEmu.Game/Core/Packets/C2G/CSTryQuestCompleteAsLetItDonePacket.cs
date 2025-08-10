using AAEmu.Commons.Network;
using AAEmu.Game.Core.Network.Game;
using AAEmu.Game.Core.Managers;
using AAEmu.Game.Core.Packets.G2C;
using AAEmu.Game.Models.Game.Quests;

namespace AAEmu.Game.Core.Packets.C2G;

public class CSTryQuestCompleteAsLetItDonePacket : GamePacket
{
    private uint _id;
    private uint _objId;
    private int _selected;

    public CSTryQuestCompleteAsLetItDonePacket() : base(CSOffsets.CSTryQuestCompleteAsLetItDonePacket, 1)
    {
        //
    }

    public override void Read(PacketStream stream)
    {
        _id = stream.ReadUInt32();
        _objId = stream.ReadBc();
        _selected = stream.ReadInt32();

        Logger.Warn($"TryQuestCompleteAsLetItDone, Id: {_id}, ObjId: {_objId}, Selected: {_selected}");

        // Check if player is actually targeting the NPC
        if (
            _objId > 0
            && Connection.ActiveChar.CurrentTarget != null
            && Connection.ActiveChar.CurrentTarget.ObjId != _objId
           )
            return;
        // Normalize selection (client may send 0)
        if (_selected <= 0)
            _selected = 1;

        // Apply early-complete transition
        Connection.ActiveChar.Quests.TryCompleteQuestAsLetItDone(_id, _selected);

        // Send an immediate quest context update to keep the UI responsive.
        if (Connection.ActiveChar.Quests.ActiveQuests.TryGetValue(_id, out Quest quest))
        {
            Connection.ActiveChar.SendPacket(new SCQuestContextUpdatedPacket(quest, quest.ComponentId));
            // Ensure prompt evaluation after the step change
            QuestManager.Instance.EnqueueEvaluation(quest);
        }
    }
}
