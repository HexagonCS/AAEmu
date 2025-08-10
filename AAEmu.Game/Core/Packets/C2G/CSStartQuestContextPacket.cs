using AAEmu.Commons.Network;
using AAEmu.Game.Core.Network.Game;
using System.Threading.Tasks;

namespace AAEmu.Game.Core.Packets.C2G;

public class CSStartQuestContextPacket() : GamePacket(CSOffsets.CSStartQuestContextPacket, 1)
{
    private uint _questContextId;
    private uint _npcObjId;
    private uint _doodadObjId;
    private uint _sphereId;

    public override void Read(PacketStream stream)
    {
        _questContextId = stream.ReadUInt32(); // questContextId
        _npcObjId = stream.ReadBc();           // npcObjId
        _doodadObjId = stream.ReadBc();        // doodadObjId
        _sphereId = stream.ReadUInt32();       // selected

        // Mirror completion handling: offload potentially heavy quest start
        // work so the network thread stays responsive and the client UI
        // doesn’t get stuck waiting on the server.
        var charRef = Connection.ActiveChar;
        var questId = _questContextId;
        var npcObjId = _npcObjId;
        var doodadObjId = _doodadObjId;
        var sphereId = _sphereId;

        Task.Run(() =>
        {
            if (npcObjId > 0)
                charRef.Quests.AddQuestFromNpc(questId, npcObjId);
            else if (doodadObjId > 0)
                charRef.Quests.AddQuestFromDoodad(questId, doodadObjId);
            else if (sphereId > 0)
                charRef.Quests.AddQuestFromSphere(questId, sphereId);
            else
                charRef.Quests.AddQuest(questId);
        });
    }
}
