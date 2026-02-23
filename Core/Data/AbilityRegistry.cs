using System.Collections.Generic;
using Autobattle.Core.Models;

namespace Autobattle.Core.Data
{
    public static class AbilityRegistry
    {
        public static readonly Dictionary<string, Ability> Abilities = new()
        {
            // Example: { "basic_attack", new Ability { Id = "basic_attack", Name = "Basic Attack", Description = "Deal physical damage.", ArchetypeId = "warrior" } }
        };
    }
}