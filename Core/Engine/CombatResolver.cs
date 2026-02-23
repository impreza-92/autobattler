using Autobattle.Core.Models;

namespace Autobattle.Core.Engine
{
    public static class CombatResolver
    {
        // Resolves a combat action and returns the resulting BattleEvent(s)
        public static BattleEvent Resolve(string actionId, Unit source, Unit target, BattleState state)
        {
            // TODO: Implement damage calculation pipeline
            return new BattleEvent { EventType = "noop", SourceUnitId = source.Id, TargetUnitId = target.Id, Payload = "" };
        }
    }
}