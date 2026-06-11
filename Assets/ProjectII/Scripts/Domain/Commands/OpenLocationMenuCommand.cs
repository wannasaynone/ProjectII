using System;
using Cysharp.Threading.Tasks;
using ProjectII.Gameplay.Application;
using ProjectII.Gameplay.Data;

namespace ProjectII.Gameplay.Domain.Commands
{
    /// <summary>
    /// OpenLocationMenu()：開啟移動選單讓玩家選擇地點；取消則不移動。
    /// 實際的 EnterLocation 事件由流程層在指令串結束後統一觸發。
    /// </summary>
    public class OpenLocationMenuCommand : KahaGameCore.Package.EffectProcessor.EffectCommandBase
    {
        private readonly ILocationService locationService;
        private readonly ILocationMenuPresenter locationMenuPresenter;

        public OpenLocationMenuCommand(ILocationService locationService, ILocationMenuPresenter locationMenuPresenter)
        {
            this.locationService = locationService;
            this.locationMenuPresenter = locationMenuPresenter;
        }

        public override void Process(string[] vars, Action onCompleted, Action onForceQuit)
        {
            SelectAsync(onCompleted).Forget();
        }

        private async UniTaskVoid SelectAsync(Action onCompleted)
        {
            LocationData selected = await locationMenuPresenter.SelectLocationAsync(locationService.GetSelectableLocations());
            if (selected != null)
            {
                locationService.MoveTo(selected.ID);
            }

            onCompleted?.Invoke();
        }
    }
}
