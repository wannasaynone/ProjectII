using System;
using System.Collections.Generic;
using KahaGameCore.Package.GameFlowSystem;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectII.Gameplay.Presentation.Views
{
    /// <summary>
    /// 懸浮子行動選單：開啟時由呼叫端指定要顯示的 MenuGroup，從整份 entry 篩出成員，於指定錨點顯示，
    /// 由本 View 自行排版（預設由上往下垂直堆疊）。點背景 backdrop 可不選擇直接關閉。
    /// 刻意不繼承 AView——子選單需可即時多開／重開，毋須 AView 的淡入淡出與單一 overlay 生命週期。
    /// 同一個實例可被不同 opener 以不同群組重複使用。
    /// </summary>
    public class SubActionMenuView : MonoBehaviour
    {
        [Tooltip("浮動面板的 RectTransform（會被設定 anchoredPosition）。")]
        [SerializeField] private RectTransform root;
        [SerializeField] private RectTransform buttonContainer;
        [SerializeField] private ActionButtonItem buttonPrefab;
        [Tooltip("全螢幕透明遮罩按鈕；點擊＝不選擇關閉。可留空。")]
        [SerializeField] private Button backdrop;
        [Tooltip("子按鈕由上往下堆疊的間距（anchoredPosition Y）。")]
        [SerializeField] private float buttonSpacing = 200f;

        private readonly List<ActionButtonItem> spawnedButtons = new List<ActionButtonItem>();

        /// <summary>顯示子選單，內容為 allEntries 中 MenuGroup 等於 menuGroup 的成員。回傳 false 代表無可見項、未開啟。</summary>
        public bool Open(
            string menuGroup,
            IReadOnlyList<ActionMenuEntry> allEntries,
            Action<ActionMenuEntry> onSelected,
            Vector2 anchoredPosition,
            Action onDismiss)
        {
            ClearButtons();

            List<ActionMenuEntry> myEntries = new List<ActionMenuEntry>();
            string targetGroup = ActionButtonSpawner.Normalize(menuGroup);
            foreach (ActionMenuEntry entry in allEntries)
            {
                if (ActionButtonSpawner.GroupOf(entry.Action) == targetGroup)
                {
                    myEntries.Add(entry);
                }
            }

            if (myEntries.Count == 0)
            {
                gameObject.SetActive(false);
                return false;
            }

            root.anchoredPosition = anchoredPosition;

            ActionButtonSpawner.Spawn(buttonContainer, buttonPrefab, myEntries, onSelected, spawnedButtons);
            for (int i = 0; i < spawnedButtons.Count; i++)
            {
                ((RectTransform)spawnedButtons[i].transform).anchoredPosition = new Vector2(0f, -i * buttonSpacing);
            }

            if (backdrop != null)
            {
                backdrop.onClick.RemoveAllListeners();
                backdrop.onClick.AddListener(() => onDismiss?.Invoke());
            }

            gameObject.SetActive(true);
            return true;
        }

        public void Close()
        {
            if (backdrop != null)
            {
                backdrop.onClick.RemoveAllListeners();
            }
            ClearButtons();
            gameObject.SetActive(false);
        }

        private void ClearButtons()
        {
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
