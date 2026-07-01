using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using KahaGameCore.Package.GameFlowSystem;
using ProjectII.Gameplay.Presentation.Views;

namespace ProjectII.Gameplay.Presentation.Presenters
{
    /// <summary>
    /// 演出協調器（上層管理器）：把「顯示行動選單」與「黑幕揭露」統籌為單一時序。
    /// 對話/換場退場後，幕控（CinematicDialoguePlayer）停在黑幕；本協調器在黑幕下把選單備妥，
    /// 再請幕控揭露，讓場景與選單一起淡出。作為 IActionMenuPresenter 傳給流程，流程層不需認得黑幕。
    ///
    /// 為何自持選單 View 而非包住舊 ActionMenuPresenter：揭露必須發生在 view.Show 完成後、
    /// await 玩家選擇之前，這個中間點在舊簡報器的 SelectActionAsync 內部，無法從外部黑箱插入，
    /// 故由本類自行掌控 Bind→Show→Reveal→await→Hide 的順序。
    /// </summary>
    public class GameplayPresentationCoordinator : IActionMenuPresenter
    {
        private readonly ActionMenuView view;
        private CinematicDialoguePlayer curtain;
        private UniTaskCompletionSource<IGameFlowAction> pendingSelection;

        public GameplayPresentationCoordinator(ActionMenuView view)
        {
            this.view = view ? view : throw new ArgumentNullException(nameof(view));
        }

        /// <summary>由組裝根於 builder.Build() 後注入幕控（幕控在 Build 期間才由對話工廠建立）。</summary>
        public void SetCinematic(CinematicDialoguePlayer cinematic)
        {
            curtain = cinematic ?? throw new ArgumentNullException(nameof(cinematic));
        }

        public async UniTask<IGameFlowAction> SelectActionAsync(IReadOnlyList<ActionMenuEntry> entries)
        {
            CancelPending();
            pendingSelection = new UniTaskCompletionSource<IGameFlowAction>();

            // 先讓進行中的退場（park）與換場收斂到黑幕，選單才在黑幕下備妥；否則延後且不檢查 state 的 park
            // 會在揭露前後蓋幕，造成「開場對話結束後黑幕不淡出、卡黑」。純數值行動無 pending → 即刻返回。
            if (curtain != null)
            {
                await curtain.WaitForPendingTransitionsAsync();
            }

            view.Bind(entries, entry => pendingSelection.TrySetResult(entry.Action));
            await view.Show(CancellationToken.None);

            // 選單已在黑幕下備妥 → 揭露場景（RevealSceneAsync 內含等待進行中的換場）；
            // 若當下並非黑幕（上一個行動沒有對話/換場、場景本就可見），RevealSceneAsync 為 no-op，選單如常直接顯示。
            if (curtain != null)
            {
                await curtain.RevealSceneAsync();
            }

            IGameFlowAction selected = await pendingSelection.Task;

            await view.Hide(CancellationToken.None);
            return selected;
        }

        /// <summary>結束遊戲流程時呼叫，先拆掉殘留的懸浮子選單，再讓等待中的選擇以 null 收斂以利流程退出。</summary>
        public void CancelPending()
        {
            view.TearDownSubMenus();
            pendingSelection?.TrySetResult(null);
        }
    }
}
