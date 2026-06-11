using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using ProjectII.Gameplay.Data;
using ProjectII.Gameplay.Domain;
using UnityEngine;

namespace ProjectII.Gameplay.Application
{
    /// <summary>
    /// 遊戲主流程。流程骨架固定為：
    ///   開新遊戲 → GameStart 事件 → ┐
    ///   ┌──────────────────────────┘
    ///   │ 階段開始 → PhaseStart 事件 → （可行動階段）行動選擇 → 行動指令 → AfterAction 事件 → …
    ///   └─ 階段切換（由表中 AdvanceTime / SetPhase 指令推動）後回到階段開始
    /// 所有劇情、條件與數值變化都由表格（GameEventTriggerData 等）定義，本類別不含任何劇情內容。
    /// </summary>
    public class GameFlowController
    {
        private readonly IGameState gameState;
        private readonly ITimeService timeService;
        private readonly ILocationService locationService;
        private readonly IPlayerActionProvider actionProvider;
        private readonly IGameEventTriggerService triggerService;
        private readonly ICommandExecutor commandExecutor;
        private readonly IActionMenuPresenter actionMenuPresenter;

        /// <summary>最後一次已觸發 EnterLocation 事件的地點，用於偵測指令造成的移動。</summary>
        private int lastEnteredLocationId;

        public GameFlowController(
            IGameState gameState,
            ITimeService timeService,
            ILocationService locationService,
            IPlayerActionProvider actionProvider,
            IGameEventTriggerService triggerService,
            ICommandExecutor commandExecutor,
            IActionMenuPresenter actionMenuPresenter)
        {
            this.gameState = gameState ?? throw new ArgumentNullException(nameof(gameState));
            this.timeService = timeService ?? throw new ArgumentNullException(nameof(timeService));
            this.locationService = locationService ?? throw new ArgumentNullException(nameof(locationService));
            this.actionProvider = actionProvider ?? throw new ArgumentNullException(nameof(actionProvider));
            this.triggerService = triggerService ?? throw new ArgumentNullException(nameof(triggerService));
            this.commandExecutor = commandExecutor ?? throw new ArgumentNullException(nameof(commandExecutor));
            this.actionMenuPresenter = actionMenuPresenter ?? throw new ArgumentNullException(nameof(actionMenuPresenter));
        }

        public async UniTask RunNewGameAsync(CancellationToken token)
        {
            gameState.ResetToInitial();
            timeService.ResetToFirstPhase();
            lastEnteredLocationId = locationService.CurrentLocationID;

            await triggerService.RaiseTimingAsync(GameFlowTimings.GameStart, token);
            await RaiseLocationTimingsIfMovedAsync(token);

            while (!token.IsCancellationRequested)
            {
                await RunPhaseAsync(token);
            }
        }

        private async UniTask RunPhaseAsync(CancellationToken token)
        {
            TimePhaseData phase = timeService.CurrentPhase;

            await triggerService.RaiseTimingAsync(GameFlowTimings.PhaseStart(phase.Key), token);
            await RaiseLocationTimingsIfMovedAsync(token);

            // 事件指令（SetPhase 等）可能已切換階段，直接進入新階段。
            if (token.IsCancellationRequested || timeService.CurrentPhase.ID != phase.ID)
            {
                return;
            }

            if (phase.AllowAction != 1)
            {
                timeService.AdvanceTime();
                return;
            }

            while (!token.IsCancellationRequested && timeService.CurrentPhase.ID == phase.ID)
            {
                await RunActionRoundAsync(token);
            }
        }

        private async UniTask RunActionRoundAsync(CancellationToken token)
        {
            IReadOnlyList<ActionMenuEntry> entries = BuildActionMenuEntries();
            if (entries.Count == 0)
            {
                Debug.LogWarning($"[GameFlowController] 地點 {locationService.CurrentLocationID} 於階段 {timeService.CurrentPhase.Key} 沒有任何可選行動，自動推進時間以避免卡死。請檢查 PlayerActionData 表。");
                timeService.AdvanceTime();
                return;
            }

            PlayerActionData chosenAction = await actionMenuPresenter.SelectActionAsync(entries);
            if (chosenAction == null)
            {
                return;
            }

            await commandExecutor.ExecuteAsync(chosenAction.Commands);
            await triggerService.RaiseTimingAsync(GameFlowTimings.AfterAction(chosenAction.ID), token);
            await RaiseLocationTimingsIfMovedAsync(token);
        }

        private IReadOnlyList<ActionMenuEntry> BuildActionMenuEntries()
        {
            return actionProvider
                .GetVisibleActions(locationService.CurrentLocationID)
                .Select(action => new ActionMenuEntry(action, actionProvider.IsEnabled(action)))
                .ToList();
        }

        /// <summary>
        /// 指令（MoveToLocation / OpenLocationMenu）移動地點後，補發 EnterLocation 事件。
        /// 事件本身又可能再移動地點（例如被送回家），因此以迴圈處理直到穩定。
        /// </summary>
        private async UniTask RaiseLocationTimingsIfMovedAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested && locationService.CurrentLocationID != lastEnteredLocationId)
            {
                lastEnteredLocationId = locationService.CurrentLocationID;
                await triggerService.RaiseTimingAsync(GameFlowTimings.EnterLocation(lastEnteredLocationId), token);
            }
        }
    }
}
