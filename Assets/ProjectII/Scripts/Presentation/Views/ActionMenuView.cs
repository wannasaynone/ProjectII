using System;
using System.Collections.Generic;
using KahaGameCore.GameEvent;
using KahaGameCore.Package.GameFlowSystem;
using KahaGameCore.UserInterfaceSystem;
using ProjectII.Gameplay.Presentation.Events;
using UnityEngine;

namespace ProjectII.Gameplay.Presentation.Views
{
    /// <summary>
    /// 行動選單：依表格資料動態產生本選單群組（<see cref="menuGroup"/>，根選單留空）的行動按鈕。
    /// 子選單由靜態 opener 按鈕（掛 <see cref="SubMenuOpener"/>）以群組字串呼叫 <see cref="OpenSubMenu"/> 開啟，
    /// 不論在根或子選單，任何 leaf 被點都收斂同一個選擇；本 View 只是把同一個 onSelected 往下傳。
    /// </summary>
    public class ActionMenuView : AView
    {
        [SerializeField] private RectTransform buttonContainer;
        [SerializeField] private ActionButtonItem buttonPrefab;
        [Tooltip("本選單顯示哪個 MenuGroup；根選單留空。")]
        [SerializeField] private string menuGroup = "";

        private readonly List<ActionButtonItem> spawnedButtons = new List<ActionButtonItem>();
        private readonly List<SubActionMenuView> openSubMenus = new List<SubActionMenuView>();
        private IReadOnlyList<ActionMenuEntry> allEntries;
        private Action<ActionMenuEntry> wrappedOnSelected;

        // 顯示中才接收開啟請求；隱藏時（AView.Hide 會 SetActive(false)）自動退訂。
        private void OnEnable()
        {
            EventBus.Subscribe<OpenSubActionMenuRequestedEvent>(OnOpenSubMenuRequested);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<OpenSubActionMenuRequestedEvent>(OnOpenSubMenuRequested);
        }

        private void OnOpenSubMenuRequested(OpenSubActionMenuRequestedEvent request)
        {
            OpenSubMenu(request.SubMenu, request.MenuGroup, request.AnchoredPosition);
        }

        public void Bind(IReadOnlyList<ActionMenuEntry> entries, Action<ActionMenuEntry> onSelected)
        {
            ClearButtons();

            allEntries = entries;
            wrappedOnSelected = entry =>
            {
                // 任何 leaf 收斂前先關掉殘留的子選單。
                CloseAllSubMenus();
                onSelected?.Invoke(entry);
            };

            string targetGroup = ActionButtonSpawner.Normalize(menuGroup);
            List<ActionMenuEntry> myEntries = new List<ActionMenuEntry>();
            foreach (ActionMenuEntry entry in entries)
            {
                if (ActionButtonSpawner.GroupOf(entry.Action) == targetGroup)
                {
                    myEntries.Add(entry);
                }
            }

            ActionButtonSpawner.Spawn(buttonContainer, buttonPrefab, myEntries, wrappedOnSelected, spawnedButtons);
            for (int i = 0; i < spawnedButtons.Count; i++)
            {
                ((RectTransform)spawnedButtons[i].transform).anchoredPosition = myEntries[i].Action.AnchoredPosition;
            }
        }

        /// <summary>
        /// 由 opener 按鈕呼叫：開啟 <paramref name="subMenu"/>，內容為目前可見行動中 MenuGroup 等於
        /// <paramref name="menuGroup"/> 的成員，顯示於 <paramref name="anchoredPosition"/>。子選單裡的 leaf
        /// 收斂的是與根選單相同的選擇。維持單一展開：開新的前先收掉其他。
        /// </summary>
        public void OpenSubMenu(SubActionMenuView subMenu, string menuGroup, Vector2 anchoredPosition)
        {
            if (subMenu == null)
            {
                return;
            }
            if (allEntries == null || wrappedOnSelected == null)
            {
                Debug.LogWarning("[ActionMenuView] 選單尚未 Bind，無法開啟子選單。");
                return;
            }

            CloseAllSubMenus();

            bool opened = subMenu.Open(menuGroup, allEntries, wrappedOnSelected, anchoredPosition, CloseAllSubMenus);
            if (opened)
            {
                openSubMenus.Add(subMenu);
            }
            else
            {
                Debug.LogWarning($"[ActionMenuView] 群組「{menuGroup}」無可見項，不開啟子選單。");
            }
        }

        /// <summary>外部（presenter 取消）強制拆掉所有展開的子選單。</summary>
        public void TearDownSubMenus()
        {
            CloseAllSubMenus();
        }

        private void CloseAllSubMenus()
        {
            foreach (SubActionMenuView subMenu in openSubMenus)
            {
                if (subMenu != null)
                {
                    subMenu.Close();
                }
            }
            openSubMenus.Clear();
        }

        private void ClearButtons()
        {
            CloseAllSubMenus();

            foreach (ActionButtonItem button in spawnedButtons)
            {
                if (button != null)
                {
                    Destroy(button.gameObject);
                }
            }
            spawnedButtons.Clear();
        }
    }
}
