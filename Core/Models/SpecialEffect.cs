using System.Collections.Generic;
using Autobattle.Core.Enums;

namespace Autobattle.Core.Models
{
    public class SpecialEffect
    {
        public SpecialEffectType Type { get; init; }
        public Dictionary<string, object> Params { get; init; } // { "amount": 0.5 } or { "buffId": "shield_wall", "duration": 3 }
    }
}
