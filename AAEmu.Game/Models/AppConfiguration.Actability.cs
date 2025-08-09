using System.Collections.Generic;

namespace AAEmu.Game.Models;

public partial class AppConfiguration
{
    public List<ActabilityRankBuffRule> ActabilityRankBuffs { get; set; } = [];

    public class ActabilityRankBuffRule
    {
        public uint ActabilityId { get; set; }
        public int MinStep { get; set; }
        public uint BuffId { get; set; }
    }
}

