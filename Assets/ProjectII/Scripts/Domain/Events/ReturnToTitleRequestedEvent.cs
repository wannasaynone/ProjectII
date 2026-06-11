using KahaGameCore.GameEvent;

namespace ProjectII.Gameplay.Domain.Events
{
    /// <summary>要求結束目前遊戲流程並返回主標題（由 ReturnToTitle 指令發出）。</summary>
    public class ReturnToTitleRequestedEvent : GameEventBase
    {
    }
}
