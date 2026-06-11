using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using ProjectII.Gameplay.Data;

namespace ProjectII.Gameplay.Application
{
    /// <summary>顯示移動選單並等待玩家選擇；回傳 null 代表取消（返回）。</summary>
    public interface ILocationMenuPresenter
    {
        UniTask<LocationData> SelectLocationAsync(IReadOnlyList<LocationData> locations);
    }
}
