using System;
using Cysharp.Threading.Tasks;
using KahaGameCore.Package.GameFlowSystem.DefaultImplements;

namespace ProjectII.Gameplay.Presentation.Presenters
{
    /// <summary>
    /// 演出「進場景」：把開場黑幕淡出、揭露 HUD/場景。事件表以 PrePerformance 引用本演出。
    /// 放在對話前＝場景先行；不放＝對話在黑幕中先演（prologue），結束後再由對話復原揭露場景。
    /// 黑/HUD 狀態仍由 CinematicDialoguePlayer 單一持有，本演出僅是薄轉接。
    /// </summary>
    public class EnterScenePerformance : IStagePerformance
    {
        private readonly CinematicDialoguePlayer cinematic;

        public EnterScenePerformance(CinematicDialoguePlayer cinematic)
        {
            this.cinematic = cinematic ?? throw new ArgumentNullException(nameof(cinematic));
        }

        public UniTask PlayAsync()
        {
            return cinematic.RevealSceneAsync();
        }
    }
}
