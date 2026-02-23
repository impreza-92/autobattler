using System.Collections.Generic;
using Autobattle.Core.Models;

namespace Autobattle.Core.Engine
{
    public static class BattleEngine
    {
        // Pure function: advances the battle state by one tick
        public static (BattleState newState, List<BattleEvent> events) Tick(BattleState state)
        {
            // TODO: Implement FSM phase handling, gauge fill, gambit evaluation, action execution, event emission
            return (state, new List<BattleEvent>());
        }
    }
}