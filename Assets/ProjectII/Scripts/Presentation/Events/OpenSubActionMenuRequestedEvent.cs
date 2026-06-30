using KahaGameCore.GameEvent;
using ProjectII.Gameplay.Presentation.Views;
using UnityEngine;

namespace ProjectII.Gameplay.Presentation.Events
{
    /// <summary>
    /// 要求開啟一個懸浮子行動選單。可由 <see cref="SubMenuOpener"/> 或任何外部系統發出；
    /// 顯示中的 <see cref="ActionMenuView"/> 會接收並以目前可見行動開啟指定群組的子選單。
    /// </summary>
    public class OpenSubActionMenuRequestedEvent : GameEventBase
    {
        /// <summary>要顯示內容的浮動子選單 View。</summary>
        public SubActionMenuView SubMenu { get; }
        /// <summary>要顯示哪個 MenuGroup 的行動。</summary>
        public string MenuGroup { get; }
        /// <summary>子選單面板的 anchoredPosition（通常＝開啟器按鈕位置 + offset）。</summary>
        public Vector2 AnchoredPosition { get; }

        public OpenSubActionMenuRequestedEvent(SubActionMenuView subMenu, string menuGroup, Vector2 anchoredPosition)
        {
            SubMenu = subMenu;
            MenuGroup = menuGroup;
            AnchoredPosition = anchoredPosition;
        }
    }
}
