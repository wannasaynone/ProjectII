using System.Threading;
using Cysharp.Threading.Tasks;

namespace ProjectII.Gameplay.Domain
{
    /// <summary>
    /// 在指定時機點檢查 GameEventTriggerData 表，依優先度依序執行命中的事件
    /// （前演出 → 劇情對話 → 效果指令 → 後演出）。
    /// 表中 Timing 可寫精確時機（如 PhaseStart:Morning），或以保留字 Any
    /// （如 PhaseStart:Any、AfterAction:Any）命中同類別的所有時機。
    /// </summary>
    public interface IGameEventTriggerService
    {
        /// <param name="cancellationToken">
        /// 流程中止訊號（如 ReturnToTitle）。取消後不再執行佇列中剩餘的事件。
        /// </param>
        UniTask RaiseTimingAsync(string timing, CancellationToken cancellationToken = default);
    }
}
