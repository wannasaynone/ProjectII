using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using ProjectII.Gameplay.Application;
using ProjectII.Gameplay.Data;
using ProjectII.Gameplay.Presentation.Views;

namespace ProjectII.Gameplay.Presentation.Presenters
{
    public class ActionMenuPresenter : IActionMenuPresenter
    {
        private readonly ActionMenuView view;
        private UniTaskCompletionSource<PlayerActionData> pendingSelection;

        public ActionMenuPresenter(ActionMenuView view)
        {
            this.view = view ? view : throw new ArgumentNullException(nameof(view));
        }

        public async UniTask<PlayerActionData> SelectActionAsync(IReadOnlyList<ActionMenuEntry> entries)
        {
            CancelPending();
            pendingSelection = new UniTaskCompletionSource<PlayerActionData>();

            view.Bind(entries, entry => pendingSelection.TrySetResult(entry.Action));
            await view.Show(CancellationToken.None);

            PlayerActionData selected = await pendingSelection.Task;

            await view.Hide(CancellationToken.None);
            return selected;
        }

        /// <summary>結束遊戲流程時呼叫，讓等待中的選擇以 null 結束以利流程退出。</summary>
        public void CancelPending()
        {
            pendingSelection?.TrySetResult(null);
        }
    }
}
