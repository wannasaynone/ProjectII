using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using KahaGameCore.Package.GameFlowSystem.DefaultImplements;
using KahaGameCore.UserInterfaceSystem;
using ProjectBSR.DialogueSystem.View;

namespace ProjectII.Gameplay.Presentation
{
    /// <summary>
    /// 對話播放器的演出裝飾器：在任何對話前後加上「黑幕 → 關 HUD → 對話 → 黑幕 → 開 HUD」。
    /// 因所有對話入口（DialogueID 欄、StartDialogue 指令）都收斂到同一個 IDialoguePlayer，
    /// 包這一層即對全部對話生效。
    ///
    /// 連續對話不重複黑幕：對話結束時的「復原」會延後一影格且可被取消，若下一段對話在此之前
    /// 就進場，便取消復原、維持在同一場黑幕內無縫銜接；只有真正回到可遊玩狀態才復原 HUD。
    /// 本類別屬於 ProjectII 演出層，黑幕/HUD 皆走 UserInterfaceController，套件不需認得 UI 型別。
    /// </summary>
    public class CinematicDialoguePlayer : IDialoguePlayer
    {
        private enum State
        {
            Gameplay, // 一般遊玩：HUD 顯示、無黑幕
            Covered,  // 已黑幕、HUD 關，尚未淡出
            Revealed  // 已淡出、對話可見
        }

        private readonly IDialoguePlayer inner;
        private readonly UserInterfaceController uiController;
        private readonly DialogueView dialogueView;

        private State state = State.Gameplay;
        private CancellationTokenSource exitCts;
        private UniTask? activeRestore;

        public CinematicDialoguePlayer(IDialoguePlayer inner, UserInterfaceController uiController, DialogueView dialogueView)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            this.uiController = uiController ? uiController : throw new ArgumentNullException(nameof(uiController));
            this.dialogueView = dialogueView ? dialogueView : throw new ArgumentNullException(nameof(dialogueView));
        }

        public async UniTask PlayAsync(int dialogueId)
        {
            await EnterAsync();
            // inner 會 SetActive(true) 並起始第一句，回傳的 task 在對話結束時完成。
            UniTask play = inner.PlayAsync(dialogueId);
            await RevealAsync();
            await play;
            RequestExit();
        }

        /// <summary>進入演出：確保已黑幕並關閉 HUD。已在演出中則僅取消待復原（不再黑幕第二次）。</summary>
        private async UniTask EnterAsync()
        {
            // 取消待復原；若復原已越過防抖點而開始執行，等它跑完再進場（避免並行衝突）。
            exitCts?.Cancel();
            exitCts?.Dispose();
            exitCts = null;
            if (activeRestore.HasValue)
            {
                await activeRestore.Value;
                activeRestore = null;
            }

            if (state != State.Gameplay)
            {
                return;
            }

            await uiController.BlackIn();
            uiController.SetTopViewActive(false);
            state = State.Covered;
        }

        /// <summary>淡出黑幕露出對話。僅在剛進場（Covered）時執行，連續對話時為 no-op，避免黑屏閃爍。</summary>
        private async UniTask RevealAsync()
        {
            if (state != State.Covered)
            {
                return;
            }

            await uiController.BlackOut();
            state = State.Revealed;
        }

        /// <summary>排程退場復原（延後且可取消）；若下一段對話在防抖視窗內進場則取消。</summary>
        private void RequestExit()
        {
            exitCts?.Cancel();
            exitCts?.Dispose();
            exitCts = new CancellationTokenSource();
            // Preserve：本 task 可能被後續 EnterAsync 再次 await，避免 UniTask 單次消費的限制。
            activeRestore = RestoreAsync(exitCts.Token).Preserve();
        }

        private async UniTask RestoreAsync(CancellationToken token)
        {
            // 只有「防抖延遲」可被取消：同影格內接連的下一段對話會在此取消，達成無縫銜接、零視覺變化。
            try
            {
                await UniTask.NextFrame(token);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            // 越過防抖點即視為真的要回到遊玩，整段復原跑完（不再中途取消，避免半黑殘留與並行衝突）。
            await uiController.BlackIn();
            dialogueView.gameObject.SetActive(false);
            uiController.SetTopViewActive(true);
            await uiController.BlackOut();
            state = State.Gameplay;
            activeRestore = null;
        }

        /// <summary>強制歸位（例如對話中途返回標題）：取消待復原、清除黑幕、狀態回遊玩。</summary>
        public void ResetTransition()
        {
            exitCts?.Cancel();
            exitCts?.Dispose();
            exitCts = null;
            activeRestore = null;
            state = State.Gameplay;
            dialogueView.gameObject.SetActive(false);
            // BlackOut 回傳 Task（非 UniTask），以 discard 方式 fire-and-forget 清除黑幕。
            _ = uiController.BlackOut();
        }
    }
}
