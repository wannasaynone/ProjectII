using System;
using System.Collections.Generic;
using KahaGameCore.Package.GameFlowSystem;
using UnityEngine;

namespace ProjectII.Gameplay.Presentation.Views
{
    /// <summary>
    /// 把一排 <see cref="ActionMenuEntry"/> 實體化成 <see cref="ActionButtonItem"/> 的共用工具。
    /// 只負責實體化與綁定點擊；按鈕的定位（排版）交給各 View 自行決定。
    /// </summary>
    public static class ActionButtonSpawner
    {
        public static void Spawn(
            RectTransform container,
            ActionButtonItem prefab,
            IReadOnlyList<ActionMenuEntry> entries,
            Action<ActionMenuEntry> onClicked,
            List<ActionButtonItem> spawnedOut)
        {
            foreach (ActionMenuEntry entry in entries)
            {
                ActionButtonItem button = UnityEngine.Object.Instantiate(prefab, container);
                button.Bind(entry, onClicked);
                spawnedOut.Add(button);
            }
        }

        /// <summary>正規化群組名稱：null / 空白皆視為根群組 ""，其餘去除前後空白（大小寫精確比對）。</summary>
        public static string Normalize(string menuGroup)
        {
            return string.IsNullOrWhiteSpace(menuGroup) ? string.Empty : menuGroup.Trim();
        }

        public static string GroupOf(IGameFlowAction action)
        {
            return Normalize(action.MenuGroup);
        }
    }
}
