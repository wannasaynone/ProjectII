using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using KahaGameCore.UserInterfaceSystem;
using TMPro;
using UnityEngine;

namespace ProjectII.Gameplay.Presentation.Views
{
    /// <summary>
    /// 遊戲主 HUD：天數/時段、數值狀態列、自言自語浮動文字。
    /// 顯示哪些數值由 GameValueData 表的 ShowInHUD 欄位決定。
    /// </summary>
    public class GameplayHudView : AView
    {
        [Tooltip("HUD 後方的常駐背景層，隨地點切換而換 prefab。實例化的背景 prefab 會掛在此容器底下。")]
        [SerializeField] private RectTransform backgroundContainer;
        [SerializeField] private CanvasGroup backgroundGroup;
        [SerializeField] private float backgroundFadeDuration = 0.3f;
        [SerializeField] private TextMeshProUGUI dayPhaseText;
        [SerializeField] private RectTransform statContainer;
        [SerializeField] private StatValueItem statItemPrefab;
        [SerializeField] private CanvasGroup monologueGroup;
        [SerializeField] private TextMeshProUGUI monologueText;
        [SerializeField] private float monologueFadeDuration = 0.3f;
        [SerializeField] private float monologueHoldDuration = 2.5f;

        private readonly Dictionary<string, StatValueItem> tagToStatItem = new Dictionary<string, StatValueItem>();
        private CancellationTokenSource monologueCts;
        private CancellationTokenSource backgroundCts;
        private GameObject currentBackgroundInstance;

        public void SetDayPhase(string text)
        {
            dayPhaseText.text = text;
        }

        /// <summary>切換 HUD 後方背景：淡出舊 prefab → 銷毀並實例化新 prefab → 淡入。</summary>
        public void SetBackground(GameObject prefab)
        {
            backgroundCts?.Cancel();
            backgroundCts?.Dispose();
            backgroundCts = new CancellationTokenSource();
            SwapBackgroundAsync(prefab, backgroundCts.Token).Forget();
        }

        private async UniTaskVoid SwapBackgroundAsync(GameObject prefab, CancellationToken token)
        {
            if (currentBackgroundInstance != null)
            {
                await FadeBackgroundAsync(backgroundGroup.alpha, 0f, token);
                Destroy(currentBackgroundInstance);
                currentBackgroundInstance = null;
            }

            currentBackgroundInstance = Instantiate(prefab, backgroundContainer);
            if (currentBackgroundInstance.transform is RectTransform rectTransform)
            {
                rectTransform.anchorMin = Vector2.zero;
                rectTransform.anchorMax = Vector2.one;
                rectTransform.offsetMin = Vector2.zero;
                rectTransform.offsetMax = Vector2.zero;
            }

            await FadeBackgroundAsync(0f, 1f, token);
        }

        private async UniTask FadeBackgroundAsync(float from, float to, CancellationToken token)
        {
            if (backgroundFadeDuration <= 0f)
            {
                backgroundGroup.alpha = to;
                return;
            }

            float timer = 0f;
            while (timer < backgroundFadeDuration)
            {
                token.ThrowIfCancellationRequested();
                timer += Time.deltaTime;
                backgroundGroup.alpha = Mathf.Lerp(from, to, timer / backgroundFadeDuration);
                await UniTask.Yield(token);
            }

            backgroundGroup.alpha = to;
        }

        public void BindStats(IReadOnlyList<(string tag, string displayName, int value)> stats)
        {
            foreach (StatValueItem item in tagToStatItem.Values)
            {
                Destroy(item.gameObject);
            }
            tagToStatItem.Clear();

            foreach ((string tag, string displayName, int value) in stats)
            {
                StatValueItem item = Instantiate(statItemPrefab, statContainer);
                item.Bind(tag, displayName, value);
                tagToStatItem.Add(tag, item);
            }
        }

        public bool TryUpdateStat(string tag, int value)
        {
            if (!tagToStatItem.TryGetValue(tag, out StatValueItem item))
            {
                return false;
            }

            item.UpdateValue(value);
            return true;
        }

        public void ShowMonologue(string text)
        {
            monologueCts?.Cancel();
            monologueCts?.Dispose();
            monologueCts = new CancellationTokenSource();
            PlayMonologueAsync(text, monologueCts.Token).Forget();
        }

        private async UniTaskVoid PlayMonologueAsync(string text, CancellationToken token)
        {
            monologueText.text = text;
            monologueGroup.gameObject.SetActive(true);

            await FadeMonologueAsync(0f, 1f, token);
            await UniTask.Delay(TimeSpan.FromSeconds(monologueHoldDuration), cancellationToken: token);
            await FadeMonologueAsync(1f, 0f, token);

            monologueGroup.gameObject.SetActive(false);
        }

        private async UniTask FadeMonologueAsync(float from, float to, CancellationToken token)
        {
            float timer = 0f;
            while (timer < monologueFadeDuration)
            {
                token.ThrowIfCancellationRequested();
                timer += Time.deltaTime;
                monologueGroup.alpha = Mathf.Lerp(from, to, timer / monologueFadeDuration);
                await UniTask.Yield(token);
            }

            monologueGroup.alpha = to;
        }

        private void OnDestroy()
        {
            monologueCts?.Cancel();
            monologueCts?.Dispose();
            backgroundCts?.Cancel();
            backgroundCts?.Dispose();
            if (currentBackgroundInstance != null)
            {
                Destroy(currentBackgroundInstance);
                currentBackgroundInstance = null;
            }
        }
    }
}
