using System.Reflection;
using ProjectII.Gameplay.Presentation.Views;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace ProjectII.Editor
{
    /// <summary>
    /// Play Mode 偵錯輔助：不開 Game 視窗也能模擬基本操作，方便自動化驗證流程。
    /// </summary>
    public static class GameDebugMenu
    {
        [MenuItem("ProjectII/Debug/Click Main Menu Start")]
        public static void ClickMainMenuStart()
        {
            MainMenuView mainMenu = Object.FindFirstObjectByType<MainMenuView>();
            if (mainMenu == null)
            {
                Debug.LogWarning("[GameDebugMenu] 場景中沒有 MainMenuView。");
                return;
            }

            InvokeButtonField(mainMenu, "startButton");
        }

        [MenuItem("ProjectII/Debug/Click First Action Button")]
        public static void ClickFirstActionButton()
        {
            ActionMenuView actionMenu = Object.FindFirstObjectByType<ActionMenuView>();
            if (actionMenu == null || !actionMenu.gameObject.activeInHierarchy)
            {
                Debug.LogWarning("[GameDebugMenu] 行動選單未顯示。");
                return;
            }

            Button firstButton = actionMenu.GetComponentInChildren<Button>();
            if (firstButton == null)
            {
                Debug.LogWarning("[GameDebugMenu] 行動選單沒有按鈕。");
                return;
            }

            Debug.Log($"[GameDebugMenu] 點擊行動：{firstButton.GetComponentInChildren<TMPro.TextMeshProUGUI>()?.text}");
            firstButton.onClick.Invoke();
        }

        [MenuItem("ProjectII/Debug/Advance Dialogue")]
        public static void AdvanceDialogue()
        {
            var dialogueView = Object.FindFirstObjectByType<ProjectBSR.DialogueSystem.View.DialogueView>();
            if (dialogueView == null || !dialogueView.gameObject.activeInHierarchy)
            {
                Debug.LogWarning("[GameDebugMenu] DialogueView 未顯示。");
                return;
            }

            MethodInfo method = dialogueView.GetType().GetMethod("OnInputDetected", BindingFlags.NonPublic | BindingFlags.Instance);
            method.Invoke(dialogueView, null);
            Debug.Log("[GameDebugMenu] 對話推進。");
        }

        [MenuItem("ProjectII/Debug/Force Start Game (Reflection)")]
        public static void ForceStartGame()
        {
            var launcher = Object.FindFirstObjectByType<ProjectII.Gameplay.GameLauncher>();
            if (launcher == null)
            {
                Debug.LogWarning("[GameDebugMenu] 場景中沒有 GameLauncher。");
                return;
            }

            MethodInfo method = launcher.GetType().GetMethod("StartGameAsync", BindingFlags.NonPublic | BindingFlags.Instance);
            try
            {
                object result = method.Invoke(launcher, null);
                result.GetType().GetMethod("Forget")?.Invoke(result, null);
                Debug.Log("[GameDebugMenu] StartGameAsync invoked.");
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private static void InvokeButtonField(Component target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null || !(field.GetValue(target) is Button button))
            {
                Debug.LogWarning($"[GameDebugMenu] 找不到按鈕欄位 {fieldName}。");
                return;
            }

            Debug.Log($"[GameDebugMenu] 點擊 {target.GetType().Name}.{fieldName}");
            button.onClick.Invoke();
        }
    }
}
