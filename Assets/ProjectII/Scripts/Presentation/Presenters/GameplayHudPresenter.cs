using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using KahaGameCore.GameData.Implemented;
using KahaGameCore.GameEvent;
using KahaGameCore.Package.GameFlowSystem.DefaultImplements.Data;
using KahaGameCore.Package.GameFlowSystem.DefaultImplements;
using KahaGameCore.Package.GameFlowSystem.DefaultImplements.Events;
using ProjectBSR.DialogueSystem;
using ProjectBSR.DialogueSystem.DefaultImplements;
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
        private readonly ICGProvider backgroundProvider = new AddressablesCGProvider();
        private readonly List<GameValueData> hudValueDefinitions;

        // 遞增的請求序號：快速連續切換地點時，只有最新一筆載入完成的背景會被套用。
        private int backgroundRequestId;

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
            backgroundProvider.ReleaseAll();
        }

        private void OnLocationChanged(LocationChangedEvent changedEvent)
        {
            ApplyBackground(changedEvent.Location?.Background);
        }

        private void ApplyBackground(string address)
        {
            // 序號自增使尚未完成的舊請求在回來時被丟棄，避免覆蓋較新的背景。
            int requestId = ++backgroundRequestId;
            if (string.IsNullOrEmpty(address))
            {
                return;
            }

            LoadBackgroundAsync(address, requestId).Forget();
        }

        private async UniTaskVoid LoadBackgroundAsync(string address, int requestId)
        {
            Texture2D texture = await backgroundProvider.LoadCGAsync(address);
            if (requestId != backgroundRequestId || texture == null)
            {
                return;
            }

            view.SetBackground(texture);
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
