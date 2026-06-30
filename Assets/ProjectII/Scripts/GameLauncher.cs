using System.Threading;
using Cysharp.Threading.Tasks;
using KahaGameCore.GameData.Implemented;
using KahaGameCore.GameEvent;
using KahaGameCore.Package.EffectProcessor;
using KahaGameCore.Package.GameFlowSystem;
using KahaGameCore.UserInterfaceSystem;
using ProjectBSR.DialogueSystem;
using ProjectBSR.DialogueSystem.View;
using KahaGameCore.Package.GameFlowSystem.DefaultImplements;
using KahaGameCore.Package.GameFlowSystem.DefaultImplements.Data;
using KahaGameCore.Package.GameFlowSystem.DefaultImplements.DataAccess;
using KahaGameCore.Package.GameFlowSystem.DefaultImplements.Events;
using ProjectII.Gameplay.Presentation;
using ProjectII.Gameplay.Presentation.Presenters;
using ProjectII.Gameplay.Presentation.Views;
using UnityEngine;

namespace ProjectII.Gameplay
{
    /// <summary>
    /// 組裝根（Composition Root）：載入表格資料、組裝所有服務與 Presenter、控制
    /// 「主標題 ↔ 遊戲流程」的切換。所有相依關係只在這裡建立。
    /// </summary>
    public class GameLauncher : MonoBehaviour
    {
        private const string MAIN_MENU_VIEW_PATH = "UIViews/MainMenuView";
        private const string GAMEPLAY_HUD_VIEW_PATH = "UIViews/GameplayHudView";
        private const string ACTION_MENU_VIEW_PATH = "UIViews/ActionMenuView";
        private const string LOCATION_MENU_VIEW_PATH = "UIViews/LocationMenuView";
        private const string HINT_POPUP_VIEW_PATH = "UIViews/HintPopupView";
        private const string CREDITS_VIEW_PATH = "UIViews/CreditsView";

        [SerializeField] private UserInterfaceController uiController;
        [SerializeField] private DialogueView dialogueView;
        [Tooltip("行動選單、提示視窗等覆蓋層 View 的父節點。")]
        [SerializeField] private RectTransform overlayRoot;
        [SerializeField] private string gameTitle = "Project II";
        [Tooltip("製作人員名單文字（GameTextData 表的 ID）。")]
        [SerializeField] private int creditsTextId = 950;

        private GameStaticDataManager staticDataManager;
        private GameFlowServices services;
        private ActionMenuPresenter actionMenuPresenter;
        private LocationMenuPresenter locationMenuPresenter;
        private HintPresenter hintPresenter;
        private GameplayHudPresenter hudPresenter;
        private CinematicDialoguePlayer cinematicDialoguePlayer;
        private CancellationTokenSource flowCts;
        private bool isGameRunning;

        private void Awake()
        {
            // 視窗失焦時仍持續運作（轉場與演出皆以時間驅動，暫停會卡住流程）。
            UnityEngine.Application.runInBackground = true;

            LoadStaticData();
            EventBus.Subscribe<ReturnToTitleRequestedEvent>(OnReturnToTitleRequested);
        }

        private void Start()
        {
            ShowMainMenuAsync().Forget();
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<ReturnToTitleRequestedEvent>(OnReturnToTitleRequested);
            CancelFlow();
            hudPresenter?.Dispose();
        }

        private void LoadStaticData()
        {
            staticDataManager = new GameStaticDataManager();
            GameFlowSystemBuilder.LoadDefaultTables(staticDataManager);
            staticDataManager.Add<DialogueData>(new ResourcesJsonStaticDataHandler());
        }

        private async UniTaskVoid ShowMainMenuAsync()
        {
            await uiController.ClearViewStack();
            MainMenuView mainMenu = await uiController.PushView<MainMenuView>(
                MAIN_MENU_VIEW_PATH,
                view => view.SetTitle(gameTitle));
            mainMenu.OnStartRequested += () => StartGameAsync().Forget();
        }

        private async UniTaskVoid StartGameAsync()
        {
            if (isGameRunning)
            {
                return;
            }
            isGameRunning = true;

            EnsureServicesBuilt();

            await uiController.ClearViewStack();

            // 開場以黑幕為中性起點：HUD/場景在黑幕下備妥但不主動揭露，揭露時機改由事件表的演出
            // （PrePerformance="Opening" → EnterScenePerformance）或對話復原驅動，讓對話可排在進場景之前。
            await uiController.BlackIn();

            GameplayHudView hudView = await uiController.PushView<GameplayHudView>(GAMEPLAY_HUD_VIEW_PATH);

            hudPresenter?.Dispose();
            hudPresenter = new GameplayHudPresenter(hudView, staticDataManager, services.GameState, services.TimeService, services.LocationService);

            // 開新局：先重置狀態與時段，Refresh 才會讀到正確的初始地點/數值/時段。
            services.GameState.ResetToInitial();
            services.TimeService.ResetToFirstPhase();
            hudPresenter.Refresh();

            // 關掉場景並告知裝飾器已處於 Covered；第一段對話的 EnterAsync 因而 no-op，不再閃場景。
            uiController.SetTopViewActive(false);
            cinematicDialoguePlayer.BeginCovered();

            flowCts = new CancellationTokenSource();
            services.FlowController.RunNewGameAsync(flowCts.Token).Forget();
        }

        private void EnsureServicesBuilt()
        {
            if (services != null)
            {
                return;
            }

            actionMenuPresenter = new ActionMenuPresenter(InstantiateOverlayView<ActionMenuView>(ACTION_MENU_VIEW_PATH));
            locationMenuPresenter = new LocationMenuPresenter(InstantiateOverlayView<LocationMenuView>(LOCATION_MENU_VIEW_PATH));
            hintPresenter = new HintPresenter(InstantiateOverlayView<HintPopupView>(HINT_POPUP_VIEW_PATH));

            // 全部採用 GameFlowSystem 的預設實作；有專案特殊需求時改用 Override 系列方法傳入。
            // 對話以 CinematicDialoguePlayer 裝飾：觸發對話時自動黑幕 → 關 HUD → 對話 → 黑幕 → 開 HUD。
            // 對話播放器（本專案自備的 DialoguePlayer，見 Presentation/DialoguePlayer.cs）由工廠在 builder 內
            // 取得 ICommandExecutor 後建構，再包上 CinematicDialoguePlayer 演出裝飾器。
            services = new GameFlowSystemBuilder(staticDataManager)
                .WithDialoguePlayerFactory(cmdExec =>
                    cinematicDialoguePlayer = new CinematicDialoguePlayer(
                        new DialoguePlayer(dialogueView, staticDataManager, cmdExec), uiController, dialogueView))
                .WithActionMenuPresenter(actionMenuPresenter)
                .WithHintPresenter(hintPresenter)
                .WithLocationMenuPresenter(locationMenuPresenter)
                .Build();

            RegisterPerformances();
        }

        /// <summary>
        /// 演出註冊處：之後實作好的 UGUI 演出（換日、丈夫外出、物資增加……）都在這裡
        /// 以表格使用的演出 ID 註冊。未註冊的 ID 會以佔位 Log 代替，不會卡住流程。
        /// </summary>
        private void RegisterPerformances()
        {
            CreditsView creditsView = InstantiateOverlayView<CreditsView>(CREDITS_VIEW_PATH);
            services.PerformancePlayer.Register("Credits", new CreditsPerformance(creditsView, services.TextProvider, creditsTextId));

            // 「進場景」：把開場黑幕淡出、揭露場景。開場事件以 PrePerformance="Opening" 引用＝場景先行；
            // 清空該欄則對話在黑幕中先演（prologue），結束後才揭露場景。
            services.PerformancePlayer.Register("Opening", new EnterScenePerformance(cinematicDialoguePlayer));
        }

        private T InstantiateOverlayView<T>(string resourcePath) where T : AView
        {
            T prefab = Resources.Load<T>(resourcePath);
            if (prefab == null)
            {
                Debug.LogError($"[GameLauncher] 找不到 View prefab：Resources/{resourcePath}");
                return null;
            }

            T view = Instantiate(prefab, overlayRoot);
            view.gameObject.SetActive(false);
            return view;
        }

        private void OnReturnToTitleRequested(ReturnToTitleRequestedEvent requestedEvent)
        {
            CancelFlow();

            actionMenuPresenter.CancelPending();
            locationMenuPresenter.CancelPending();
            hintPresenter.CancelPending();

            hudPresenter?.Dispose();
            hudPresenter = null;

            // 對話演出可能正卡在黑幕中途，強制歸位避免回到標題後卡黑屏。
            cinematicDialoguePlayer?.ResetTransition();

            dialogueView.gameObject.SetActive(false);
            isGameRunning = false;

            ShowMainMenuAsync().Forget();
        }

        private void CancelFlow()
        {
            if (flowCts != null)
            {
                flowCts.Cancel();
                flowCts.Dispose();
                flowCts = null;
            }
        }
    }
}
