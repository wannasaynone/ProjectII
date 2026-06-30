using KahaGameCore.GameEvent;
using ProjectII.Gameplay.Presentation.Events;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectII.Gameplay.Presentation.Views
{
    /// <summary>
    /// 掛在「靜態 opener 按鈕」上的元件：按下時發出 <see cref="OpenSubActionMenuRequestedEvent"/>，
    /// 由顯示中的 <see cref="ActionMenuView"/> 接收並開啟指定 MenuGroup 的懸浮子選單，
    /// 出現在本按鈕 anchoredPosition + <see cref="offset"/> 處。透過事件解耦：opener 不持有
    /// ActionMenuView 參照，外部系統也能發同一事件來開啟子選單。
    /// 群組字串可：(a) 填在 <see cref="menuGroup"/> 欄並指定 <see cref="button"/> 自動掛 onClick；
    /// 或 (b) 由 Button 的 OnClick() 事件直接呼叫 <see cref="Open"/>(string) 把字串填在 Inspector。
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class SubMenuOpener : MonoBehaviour
    {
        [Tooltip("要開啟內容的懸浮子選單 View。")]
        [SerializeField] private SubActionMenuView subMenu;
        [Tooltip("要開啟的 MenuGroup；也可改由 Button.OnClick 以字串參數呼叫 Open(string)。")]
        [SerializeField] private string menuGroup;
        [Tooltip("子選單相對本按鈕 anchoredPosition 的偏移。")]
        [SerializeField] private Vector2 offset;
        [Tooltip("可選：指定後會自動掛上此 Button 的 onClick（以上面的 menuGroup 開啟）。")]
        [SerializeField] private Button button;

        private void Awake()
        {
            if (button != null)
            {
                button.onClick.AddListener(OpenConfigured);
            }
        }

        private void OnDestroy()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(OpenConfigured);
            }
        }

        /// <summary>用欄位設定的 <see cref="menuGroup"/> 開啟（給自動掛的 onClick 或無參數呼叫）。</summary>
        public void OpenConfigured()
        {
            Open(menuGroup);
        }

        /// <summary>發出開啟子選單事件（給 Button.OnClick 直接以字串參數呼叫）。</summary>
        public void Open(string group)
        {
            if (subMenu == null)
            {
                Debug.LogWarning("[SubMenuOpener] 未設定 subMenu，無法開啟子選單。", this);
                return;
            }

            Vector2 anchor = ((RectTransform)transform).anchoredPosition + offset;
            EventBus.Publish(new OpenSubActionMenuRequestedEvent(subMenu, group, anchor));
        }
    }
}
