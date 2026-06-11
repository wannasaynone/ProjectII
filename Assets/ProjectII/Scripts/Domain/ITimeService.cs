using ProjectII.Gameplay.Data;

namespace ProjectII.Gameplay.Domain
{
    /// <summary>
    /// 時間流動服務。階段順序與換日規則完全由 TimePhaseData 表定義。
    /// 異動時發佈 TimePhaseChangedEvent，並同步寫入 $CurrentPhase / $Day 供條件式引用。
    /// </summary>
    public interface ITimeService
    {
        TimePhaseData CurrentPhase { get; }
        int CurrentDay { get; }

        /// <summary>重設到表中第一個階段、第一天（開新遊戲）。</summary>
        void ResetToFirstPhase();
        /// <summary>推進到下一個階段（依 NextID），必要時換日。</summary>
        void AdvanceTime();
        /// <summary>直接跳到指定階段（依 Key），不換日。</summary>
        void SetPhase(string phaseKey);
    }
}
