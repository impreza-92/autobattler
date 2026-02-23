namespace Autobattle.Core.Models
{
    public class Team
    {
        public string Id { get; init; }
        public string Name { get; init; }
        public List<Unit> Units { get; init; }
    }
}