using KahaGameCore.GameEvent;
using ProjectII.Gameplay.Data;

namespace ProjectII.Gameplay.Domain.Events
{
    public class LocationChangedEvent : GameEventBase
    {
        public LocationData Location { get; }

        public LocationChangedEvent(LocationData location)
        {
            Location = location;
        }
    }
}
