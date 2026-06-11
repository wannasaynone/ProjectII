using KahaGameCore.GameEvent;

namespace ProjectII.Gameplay.Domain.Events
{
    public class GameValueChangedEvent : GameEventBase
    {
        public string Tag { get; }
        public int NewValue { get; }

        public GameValueChangedEvent(string tag, int newValue)
        {
            Tag = tag;
            NewValue = newValue;
        }
    }
}
