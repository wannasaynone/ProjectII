using KahaGameCore.GameEvent;
using ProjectII.Gameplay.Data;

namespace ProjectII.Gameplay.Domain.Events
{
    public class TimePhaseChangedEvent : GameEventBase
    {
        public TimePhaseData Phase { get; }
        public int Day { get; }

        public TimePhaseChangedEvent(TimePhaseData phase, int day)
        {
            Phase = phase;
            Day = day;
        }
    }
}
