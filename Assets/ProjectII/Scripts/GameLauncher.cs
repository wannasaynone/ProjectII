using System.Threading;
using Cysharp.Threading.Tasks;
using KahaGameCore.GameData.Implemented;
using KahaGameCore.GameEvent;
using KahaGameCore.Package.EffectProcessor;
using KahaGameCore.UserInterfaceSystem;
using ProjectBSR.DialogueSystem;
using ProjectBSR.DialogueSystem.View;
using ProjectII.Gameplay.Application;
using ProjectII.Gameplay.Data;
using ProjectII.Gameplay.DataAccess;
using ProjectII.Gameplay.Domain;
using ProjectII.Gameplay.Domain.Events;
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
        private Services services;
        private GameplayHudPresenter hudPresenter;
        private CancellationTokenSource flowCts;
        private bool isGameRunning;

        /// <summary>跨次遊玩共用的服務群組（開新遊戲時由 GameState.ResetToInitial 重置狀態）。</summary>
        private class Services
        {
            public IGameState GameState;
            public ITimeService TimeService;
            public ILocationService LocationService;
            public IPlayerActionProvider ActionProvider;
            public IGameTextProvider TextProvider;
            public IPerformancePlayer PerformancePlayer;
            public ICommandExecutor CommandExecutor;
            public IDialoguePlayer DialoguePlayer;
            public IGameEventTriggerService TriggerService;
            public GameFlowController FlowController;
            public ActionMenuPresenter ActionMenuPresenter;
            public LocationMenuPresenter LocationMenuPresenter;
            public HintPresenter HintPresenter;
        }

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
            ResourcesJsonStaticDataHandler handler = new ResourcesJsonStaticDataHandler();
            staticDataManager.Add<DialogueData>(handler);
            staticDataManager.Add<TimePhaseData>(handler);
            staticDataManager.Add<PlayerActionData>(handler);
            staticDataManager.Add<LocationData>(handler);
            staticDataManager.Add<GameEventTriggerData>(handler);
            staticDataManager.Add<GameValueData>(handler);
            staticDataManager.Add<GameTextData>(handler);
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
            GameplayHudView hudView = await uiController.PushView<GameplayHudView>(GAMEPLAY_HUD_VIEW_PATH);

            hudPresenter?.Dispose();
            hudPresenter = new GameplayHudPresenter(hudView, staticDataManager, services.GameState, services.TimeService);
            hudPresenter.Refresh();

            flowCts = new CancellationTokenSource();
            services.FlowController.RunNewGameAsync(flowCts.Token).Forget();
        }

        private void EnsureServicesBuilt()
        {
            if (services != null)
            {
                return;
            }

            services = new Services();
            services.GameState = new GameState(staticDataManager);
            IConditionEvaluator conditionEvaluator = new FormulaConditionEvaluator(services.GameState);
            services.TimeService = new TimeService(staticDataManager, services.GameState);
            services.LocationService = new Domain.LocationService(staticDataManager, services.GameState, conditionEvaluator);
            services.ActionProvider = new PlayerActionProvider(staticDataManager, conditionEvaluator);
            services.TextProvider = new GameTextProvider(staticDataManager, conditionEvaluator);
            services.PerformancePlayer = new PerformanceRegistry();

            EffectCommandFactoryContainer factoryContainer = new EffectCommandFactoryContainer();
            services.CommandExecutor = new EffectCommandExecutor(factoryContainer, services.GameState);
            services.DialoguePlayer = new DialoguePlayer(dialogueView, staticDataManager, services.CommandExecutor);

            services.ActionMenuPresenter = new ActionMenuPresenter(InstantiateOverlayView<ActionMenuView>(ACTION_MENU_VIEW_PATH));
            services.LocationMenuPresenter = new LocationMenuPresenter(InstantiateOverlayView<LocationMenuView>(LOCATION_MENU_VIEW_PATH));
            services.HintPresenter = new HintPresenter(InstantiateOverlayView<HintPopupView>(HINT_POPUP_VIEW_PATH));

            EffectCommandRegistrar.RegisterAll(
                factoryContainer,
                services.GameState,
                services.TimeService,
                services.LocationService,
                services.DialoguePlayer,
                services.PerformancePlayer,
                services.TextProvider,
                services.HintPresenter,
                services.LocationMenuPresenter);

            RegisterPerformances();

            services.TriggerService = new GameEventTriggerService(
                staticDataManager,
                services.GameState,
                conditionEvaluator,
                services.DialoguePlayer,
                services.PerformancePlayer,
                services.CommandExecutor);

            services.FlowController = new GameFlowController(
                services.GameState,
                services.TimeService,
                services.LocationService,
                services.ActionProvider,
                services.TriggerService,
                services.CommandExecutor,
                services.ActionMenuPresenter);
        }

        /// <summary>
        /// 演出註冊處：之後實作好的 UGUI 演出（換日、丈夫外出、物資增加……）都在這裡
        /// 以表格使用的演出 ID 註冊。未註冊的 ID 會以佔位 Log 代替，不會卡住流程。
        /// </summary>
        private void RegisterPerformances()
        {
            CreditsView creditsView = InstantiateOverlayView<CreditsView>(CREDITS_VIEW_PATH);
            services.PerformancePlayer.Register("Credits", new CreditsPerformance(creditsView, services.TextProvider, creditsTextId));
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

            services.ActionMenuPresenter.CancelPending();
            services.LocationMenuPresenter.CancelPending();
            services.HintPresenter.CancelPending();

            hudPresenter?.Dispose();
            hudPresenter = null;

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
