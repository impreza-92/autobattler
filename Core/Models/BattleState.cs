using Autobattle.Core.Enums;

namespace Autobattle.Core.Models
{
    public class BattleState
    {
        public List<Team> Teams { get; init; }
        public int Tick { get; init; }
        public BattlePhase Phase { get; init; }
    }
}