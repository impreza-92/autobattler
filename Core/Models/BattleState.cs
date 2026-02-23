namespace Autobattle.Core.Models
{
    public class BattleState
    {
        public List<Team> Teams { get; init; }
        public int Tick { get; init; }
        public string Phase { get; init; } // setup, running, paused, ended
    }
}