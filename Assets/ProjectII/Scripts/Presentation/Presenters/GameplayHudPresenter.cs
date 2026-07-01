using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using KahaGameCore.GameData.Implemented;
using KahaGameCore.GameEvent;
using KahaGameCore.Package.GameFlowSystem.DefaultImplements.Data;
using KahaGameCore.Package.GameFlowSystem.DefaultImplements;
using KahaGameCore.Package.GameFlowSystem.DefaultImplements.Events;
using ProjectII.Gameplay.Presentation.Views;
using UnityEngine;

namespace ProjectII.Gameplay.Presentation.Presenters
{
    /// <summary>
    /// 監聽遊戲狀態事件並更新 HUD。
    /// HUD 上顯示哪些數值由 GameValueData 表的 ShowInHUD 欄位決定。
    /// </summary>
    public class GameplayHudPresenter : IDisposable
    {
        private readonly GameplayHudView view;
        private readonly IGameState gameState;
        private readonly ITimeService timeService;
        private readonly ILocationService locationService;
        private readonly CinematicDialoguePlayer curtain;
        private readonly List<GameValueData> hudValueDefinitions;

        public GameplayHudPresenter(
            GameplayHudView view,
            GameStaticDataManager staticDataManager,
            IGameState gameState,
            ITimeService timeService,
            ILocationService locationService,
            CinematicDialoguePlayer curtain)
        {
            this.view = view ? view : throw new ArgumentNullException(nameof(view));
            this.gameState = gameState ?? throw new ArgumentNullException(nameof(gameState));
            this.timeService = timeService ?? throw new ArgumentNullException(nameof(timeService));
            this.locationService = locationService ?? throw new ArgumentNullException(nameof(locationService));
            this.curtain = curtain ?? throw new ArgumentNullException(nameof(curtain));

            GameValueData[] definitions = staticDataManager.GetAllGameData<GameValueData>();
            hudValueDefinitions = definitions == null
                ? new List<GameValueData>()
                : definitions.Where(definition => definition.ShowInHUD == 1).OrderBy(definition => definition.ID).ToList();

            EventBus.Subscribe<GameValueChangedEvent>(OnGameValueChanged);
            EventBus.Subscribe<TimePhaseChangedEvent>(OnTimePhaseChanged);
            EventBus.Subscribe<MonologueRequestedEvent>(OnMonologueRequested);
            EventBus.Subscribe<LocationChangedEvent>(OnLocationChanged);
        }

        /// <summary>開新遊戲時重建狀態列，並套用目前地點的初始背景。</summary>
        public void Refresh()
        {
            view.BindStats(hudValueDefinitions
                .Select(definition => (definition.Tag, definition.DisplayName, gameState.Get(definition.Tag)))
                .ToList());

            UpdateDayPhaseText();

            // 開場初始背景：此時畫面已在開場黑幕下（GameLauncher.BeginCovered），直接套用、不需協調揭露。
            GameObject initialBackground = LoadBackground(locationService.CurrentLocation?.Background);
            if (initialBackground != null)
            {
                view.SetBackground(initialBackground);
            }
        }

        public void Dispose()
        {
            EventBus.Unsubscribe<GameValueChangedEvent>(OnGameValueChanged);
            EventBus.Unsubscribe<TimePhaseChangedEvent>(OnTimePhaseChanged);
            EventBus.Unsubscribe<MonologueRequestedEvent>(OnMonologueRequested);
            EventBus.Unsubscribe<LocationChangedEvent>(OnLocationChanged);
        }

        private void OnLocationChanged(LocationChangedEvent changedEvent)
        {
            GameObject prefab = LoadBackground(changedEvent.Location?.Background);
            if (prefab == null)
            {
                return;
            }

            // 換場一律在黑幕下：先確保蓋幕、再換背景，並把整段登記給幕控，令後續任何揭露先等背景換完，
            // 避免在亮著的畫面上看到背景切換。task 為熱啟動（UniTask），登記後由幕控於揭露時觀察其完成。
            curtain.RegisterSceneChange(CoverThenSwapAsync(prefab));
        }

        private async UniTask CoverThenSwapAsync(GameObject prefab)
        {
            // 被更新的換場取消（backgroundCts）時視為完成，避免孤兒 task 冒出未觀察的取消例外。
            try
            {
                await curtain.EnsureCoveredAsync();
                await view.SwapBackgroundAsync(prefab);
            }
            catch (OperationCanceledException)
            {
            }
        }

        private GameObject LoadBackground(string resourcePath)
        {
            if (string.IsNullOrEmpty(resourcePath))
            {
                return null;
            }

            GameObject prefab = Resources.Load<GameObject>(resourcePath);
            if (prefab == null)
            {
                Debug.LogError($"[GameplayHudPresenter] Failed to load background prefab: {resourcePath}");
                return null;
            }

            return prefab;
        }

        private void OnGameValueChanged(GameValueChangedEvent changedEvent)
        {
            view.TryUpdateStat(changedEvent.Tag, changedEvent.NewValue);
        }

        private void OnTimePhaseChanged(TimePhaseChangedEvent changedEvent)
        {
            UpdateDayPhaseText();
        }

        private void OnMonologueRequested(MonologueRequestedEvent requestedEvent)
        {
            view.ShowMonologue(requestedEvent.Text);
        }

        private void UpdateDayPhaseText()
        {
            if (timeService.CurrentPhase == null)
            {
                return;
            }

            view.SetDayPhase($"第 {timeService.CurrentDay} 天　{timeService.CurrentPhase.DisplayName}");
        }
    }
}
