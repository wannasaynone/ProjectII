using System;
using System.Collections.Generic;
using System.Linq;
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
        private readonly List<GameValueData> hudValueDefinitions;

        public GameplayHudPresenter(
            GameplayHudView view,
            GameStaticDataManager staticDataManager,
            IGameState gameState,
            ITimeService timeService,
            ILocationService locationService)
        {
            this.view = view ? view : throw new ArgumentNullException(nameof(view));
            this.gameState = gameState ?? throw new ArgumentNullException(nameof(gameState));
            this.timeService = timeService ?? throw new ArgumentNullException(nameof(timeService));
            this.locationService = locationService ?? throw new ArgumentNullException(nameof(locationService));

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
            ApplyBackground(locationService.CurrentLocation?.Background);
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
            ApplyBackground(changedEvent.Location?.Background);
        }

        private void ApplyBackground(string resourcePath)
        {
            if (string.IsNullOrEmpty(resourcePath))
            {
                return;
            }

            GameObject prefab = Resources.Load<GameObject>(resourcePath);
            if (prefab == null)
            {
                Debug.LogError($"[GameplayHudPresenter] Failed to load background prefab: {resourcePath}");
                return;
            }

            view.SetBackground(prefab);
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
