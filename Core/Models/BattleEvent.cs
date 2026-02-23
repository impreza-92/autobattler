namespace Autobattle.Core.Models
{
    public class BattleEvent
    {
        public string EventType { get; init; }
        public string SourceUnitId { get; init; }
        public string TargetUnitId { get; init; }
        public string Payload { get; init; } // JSON or string for event-specific data
    }
}