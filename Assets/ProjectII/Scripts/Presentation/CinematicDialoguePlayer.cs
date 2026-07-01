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
        private UniTask? activeSceneChange;

        public CinematicDialoguePlayer(IDialoguePlayer inner, UserInterfaceController uiController, DialogueView dialogueView)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            this.uiController = uiController ? uiController : throw new ArgumentNullException(nameof(uiController));
            this.dialogueView = dialogueView ? dialogueView : throw new ArgumentNullException(nameof(dialogueView));
        }

        /// <summary>開場以黑幕起手：宣告畫面已黑、HUD 已關（呼叫端需先 BlackIn + SetTopViewActive(false)）。
        /// 第一段對話的 EnterAsync 因 state != Gameplay 而 no-op，直接從黑幕淡出，不閃場景。</summary>
        public void BeginCovered()
        {
            state = State.Covered;
        }

        /// <summary>「進場景」：從黑幕揭露 HUD/場景並回到 Gameplay。供 EnterScenePerformance／行動選單協調器呼叫。
        /// 非 Covered（場景已現/對話中）則 no-op。揭露前先等待進行中的換場，避免蓋在換到一半的背景上。</summary>
        public async UniTask RevealSceneAsync()
        {
            // 先等進行中的換場（含其蓋幕）完成，再判斷狀態：換場的 BlackIn 可能尚未把 state 設為 Covered，
            // 若先判斷會誤判為非黑幕而早退，導致選單卡在黑幕後。
            await WaitForSceneChangeAsync();
            if (state != State.Covered)
            {
                return;
            }

            uiController.SetTopViewActive(true);
            await uiController.BlackOut();
            state = State.Gameplay;
        }

        /// <summary>登記一段「換場」（例如背景換 prefab）：後續任何揭露都會先等它完成，確保換場只在黑幕下發生、
        /// 內容不會蓋在換到一半的畫面上。由 GameplayHudPresenter 於 LocationChanged 時呼叫。</summary>
        public void RegisterSceneChange(UniTask sceneChange)
        {
            activeSceneChange = sceneChange.Preserve();
        }

        /// <summary>等待任何進行中的退場（park）與換場收斂，讓 state 收斂到 Covered。供行動選單協調器在「顯示選單前」呼叫，
        /// 避免延後且不檢查 state 的 park（RestoreAsync）於揭露前後蓋幕造成卡黑。無 pending 時即刻返回（例如純數值行動仍在 Gameplay）。</summary>
        public async UniTask WaitForPendingTransitionsAsync()
        {
            // 先等換場收斂：換場內的 EnsureCoveredAsync 會接手待復原（park）並將 activeRestore 清空。
            // 若反過來先在此 await activeRestore，會與換場同時 await 同一個 UniTask，
            // 觸發「Already continuation registered」（Preserve 只允許完成後重複 await，不允許並行等待）。
            await WaitForSceneChangeAsync();

            if (activeRestore.HasValue)
            {
                try
                {
                    await activeRestore.Value;
                }
                catch (OperationCanceledException)
                {
                }
                activeRestore = null;
            }
        }

        /// <summary>等待並清除進行中的換場（若有）。換場被更新的換場取消時視為完成，不讓揭露連帶失敗。</summary>
        private async UniTask WaitForSceneChangeAsync()
        {
            if (activeSceneChange.HasValue)
            {
                try
                {
                    await activeSceneChange.Value;
                }
                catch (OperationCanceledException)
                {
                }
                activeSceneChange = null;
            }
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
            // 先等進行中的換場（其蓋幕會把 state 設為 Covered），避免與換場的 BlackIn 撞在一起、
            // 誤判為 Gameplay 而黑幕第二次、把換場的蓋幕取消掉（連帶略過背景替換）。
            await WaitForSceneChangeAsync();

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

        /// <summary>淡出黑幕露出對話。僅在剛進場（Covered）時執行，連續對話時為 no-op，避免黑屏閃爍。
        /// 揭露前先等待進行中的換場，確保背景換完才露出。</summary>
        private async UniTask RevealAsync()
        {
            await WaitForSceneChangeAsync();
            if (state != State.Covered)
            {
                return;
            }

            await uiController.BlackOut();
            state = State.Revealed;
        }

        /// <summary>確保畫面已被黑幕蓋住（供背景換場等「非對話」演出在蓋幕下進行）。
        /// 與 <see cref="EnterAsync"/> 不同：不論目前在 Gameplay 或 Revealed（對話剛結束尚未 park）都會蓋幕，
        /// 已 Covered 則 no-op。取消待復原後接手蓋幕，避免與 park 並行衝突。</summary>
        public async UniTask EnsureCoveredAsync()
        {
            exitCts?.Cancel();
            exitCts?.Dispose();
            exitCts = null;
            if (activeRestore.HasValue)
            {
                await activeRestore.Value;
                activeRestore = null;
            }

            if (state == State.Covered)
            {
                return;
            }

            await uiController.BlackIn();
            uiController.SetTopViewActive(false);
            dialogueView.gameObject.SetActive(false);
            state = State.Covered;
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

            // 越過防抖點即視為真的離開對話：蓋幕收對話框、把 HUD 備妥，然後「停在黑幕」不自動揭露。
            // 揭露交給接著顯示內容的人（行動選單協調器的 RevealSceneAsync／下一段對話的 RevealAsync）。
            await uiController.BlackIn();
            dialogueView.gameObject.SetActive(false);
            uiController.SetTopViewActive(true);
            state = State.Covered;
            activeRestore = null;
        }

        /// <summary>強制歸位（例如對話中途返回標題）：取消待復原、清除黑幕、狀態回遊玩。</summary>
        public void ResetTransition()
        {
            exitCts?.Cancel();
            exitCts?.Dispose();
            exitCts = null;
            activeRestore = null;
            activeSceneChange = null;
            state = State.Gameplay;
            dialogueView.gameObject.SetActive(false);
            // BlackOut 回傳 Task（非 UniTask），以 discard 方式 fire-and-forget 清除黑幕。
            _ = uiController.BlackOut();
        }
    }
}
