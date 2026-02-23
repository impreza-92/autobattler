namespace Autobattle.Core.Models
{
    public class Unit
    {
        public string Id { get; init; }
        public string Name { get; init; }
        public int Position { get; init; } // 0-6
        public Stats Stats { get; init; }
        public int CurrentHp { get; init; }
        public float ActionGauge { get; init; }
        public List<GambitSlot> Gambits { get; init; }
        public List<ActiveStatusEffect> ActiveEffects { get; init; }
        public IReadOnlyList<string> UnlockedArchetypes { get; init; } // derived
        public IReadOnlyList<string> AvailableAbilities { get; init; } // derived
    }
}