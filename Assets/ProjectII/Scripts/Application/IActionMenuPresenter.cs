using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using ProjectII.Gameplay.Data;

namespace ProjectII.Gameplay.Application
{
    /// <summary>行動選單的一個項目。</summary>
    public class ActionMenuEntry
    {
        public PlayerActionData Action { get; }
        public bool IsEnabled { get; }

        public ActionMenuEntry(PlayerActionData action, bool isEnabled)
        {
            Action = action;
            IsEnabled = isEnabled;
        }
    }

    /// <summary>顯示行動選單並等待玩家選擇（流程層只依賴介面，不接觸 UGUI）。</summary>
    public interface IActionMenuPresenter
    {
        UniTask<PlayerActionData> SelectActionAsync(IReadOnlyList<ActionMenuEntry> entries);
    }
}
