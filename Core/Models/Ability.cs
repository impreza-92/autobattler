using System.Collections.Generic;
using Autobattle.Core.Enums;

namespace Autobattle.Core.Models
{
    public class Ability
    {
        public string Id { get; init; }
        public string Name { get; init; }
        public string Description { get; init; }
        public AbilityType Type { get; init; }
        public TargetingType Targeting { get; init; }
        public float Power { get; init; } // Multiplier for the base stat (e.g., 1.5)
        public StatId ScalingStat { get; init; }
        public bool IgnoresProtection { get; init; } // true for abilities like Backstab
        public List<SpecialEffect> Special { get; init; } // Additional effects
        public string ArchetypeId { get; init; }
    }
}