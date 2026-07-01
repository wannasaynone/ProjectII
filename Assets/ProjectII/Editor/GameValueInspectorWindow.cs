using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using KahaGameCore.GameData.Implemented;
using KahaGameCore.Package.GameFlowSystem.DefaultImplements;
using KahaGameCore.Package.GameFlowSystem.DefaultImplements.Data;
using KahaGameCore.ValueContainer;
using ProjectII.Gameplay;
using UnityEditor;
using UnityEngine;

namespace ProjectII.Editor
{
    /// <summary>
    /// 執行期數值檢視器：Play 時即時列出 <see cref="GameLauncher"/> 內 GameState 的所有當前數值
    /// （含表定義數值與 EventTriggerCount_x / LocationUnlocked_x 等動態旗標），並可直接編輯。
    /// 純開發工具；編輯經公開的 IGameState.Set → 自動鉗制上下限並發佈 GameValueChangedEvent，
    /// 現有 HUD 因而自動同步，無需額外接線。
    /// 只有三個 private 成員（GameLauncher.services / GameLauncher.staticDataManager /
    /// GameValueContainer.tagToBaseValue）以反射存取，其餘皆走公開 API。
    /// </summary>
    public class GameValueInspectorWindow : EditorWindow
    {
        private static readonly FieldInfo servicesField =
            typeof(GameLauncher).GetField("services", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo staticDataField =
            typeof(GameLauncher).GetField("staticDataManager", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo baseValueField =
            typeof(GameValueContainer).GetField("tagToBaseValue", BindingFlags.NonPublic | BindingFlags.Instance);

        private Vector2 scrollPosition;

        [MenuItem("ProjectII/Debug/Value Inspector")]
        public static void ShowWindow()
        {
            GetWindow<GameValueInspectorWindow>("數值檢視器");
        }

        private void OnEnable()
        {
            // Play 中數值變動時即時反映（EditorWindow 預設不會每幀重繪）。
            EditorApplication.update += Repaint;
        }

        private void OnDisable()
        {
            EditorApplication.update -= Repaint;
        }

        private void OnGUI()
        {
            if (servicesField == null || staticDataField == null || baseValueField == null)
            {
                EditorGUILayout.HelpBox(
                    "反射欄位遺失：GameLauncher 或 GameValueContainer 的內部結構可能已變更，請更新 GameValueInspectorWindow。",
                    MessageType.Error);
                return;
            }

            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("請先進入 Play 並開始遊戲，才會顯示當前數值。", MessageType.Info);
                return;
            }

            GameLauncher launcher = Object.FindFirstObjectByType<GameLauncher>();
            if (launcher == null)
            {
                EditorGUILayout.HelpBox("場景中找不到 GameLauncher。", MessageType.Warning);
                return;
            }

            var services = servicesField.GetValue(launcher) as GameFlowServices;
            if (services == null || services.GameState == null)
            {
                EditorGUILayout.HelpBox("服務尚未組裝——請點『開始遊戲』（或 ProjectII/Debug/Force Start Game）。", MessageType.Info);
                return;
            }

            IGameState gameState = services.GameState;
            var container = gameState.Container as GameValueContainer;
            var staticData = staticDataField.GetValue(launcher) as GameStaticDataManager;

            DrawHeader();
            DrawValues(gameState, container, staticData);
        }

        private void DrawHeader()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                GUILayout.Label("已連上 GameState", EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton))
                {
                    Repaint();
                }
            }
        }

        private void DrawValues(IGameState gameState, GameValueContainer container, GameStaticDataManager staticData)
        {
            // 表定義：建立 tag → 定義 對照，並保留 ID 排序。
            GameValueData[] definitions = staticData != null
                ? staticData.GetAllGameData<GameValueData>()
                : null;
            definitions ??= System.Array.Empty<GameValueData>();

            var definedTags = new HashSet<string>(definitions.Select(d => d.Tag));

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            // ── 區塊一：表定義數值（依 ID 排序） ──
            EditorGUILayout.LabelField("表定義數值", EditorStyles.boldLabel);
            foreach (GameValueData def in definitions.OrderBy(d => d.ID))
            {
                string label = $"{def.DisplayName} ({def.Tag})";
                string suffix = $"[{def.MinValue}~{def.MaxValue}]{(def.ShowInHUD == 1 ? " ★HUD" : string.Empty)}";
                DrawEditableValue(gameState, def.Tag, label, suffix);
            }

            // ── 區塊二：動態旗標（存在於容器但不在表上，依字母排序） ──
            List<string> dynamicTags = EnumerateBaseTags(container)
                .Where(tag => !definedTags.Contains(tag))
                .OrderBy(tag => tag)
                .ToList();

            if (dynamicTags.Count > 0)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("動態旗標（未在 GameValueData 表定義）", EditorStyles.boldLabel);
                foreach (string tag in dynamicTags)
                {
                    DrawEditableValue(gameState, tag, tag, string.Empty);
                }
            }

            // ── 區塊三：字串鍵值（唯讀） ──
            Dictionary<string, string> stringPairs = container != null
                ? container.GetAllStringKeyValuePairs()
                : null;
            if (stringPairs != null && stringPairs.Count > 0)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("字串旗標（唯讀）", EditorStyles.boldLabel);
                foreach (KeyValuePair<string, string> pair in stringPairs.OrderBy(p => p.Key))
                {
                    EditorGUILayout.LabelField(pair.Key, pair.Value);
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawEditableValue(IGameState gameState, string tag, string label, string suffix)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                int current = gameState.Get(tag);
                EditorGUI.BeginChangeCheck();
                int next = EditorGUILayout.IntField(label, current);
                if (EditorGUI.EndChangeCheck() && next != current)
                {
                    // Set 會依表定義鉗制上下限、發佈 GameValueChangedEvent（HUD 自動同步）。
                    gameState.Set(tag, next);
                }

                if (!string.IsNullOrEmpty(suffix))
                {
                    GUILayout.Label(suffix, EditorStyles.miniLabel, GUILayout.Width(120f));
                }
            }
        }

        private IEnumerable<string> EnumerateBaseTags(GameValueContainer container)
        {
            if (container == null)
            {
                return System.Array.Empty<string>();
            }

            // GameValueContainer 未公開枚舉 API，反射讀 private tagToBaseValue 的 keys。
            if (baseValueField.GetValue(container) is Dictionary<string, int> baseValues)
            {
                return baseValues.Keys.ToList();
            }

            return System.Array.Empty<string>();
        }
    }
}
