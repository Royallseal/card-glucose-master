using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CGM.UI
{
    public class TooltipManager : MonoBehaviour
    {
        public static TooltipManager Instance { get; private set; }

        [Header("描述框预制体设置")]
        [Tooltip("CustomTooltip 预制体")]
        [SerializeField] private GameObject tooltipPrefab;

        [Tooltip("预制体内部挂载的 TextMeshPro 文本组件（如 BuffDescribeText）")]
        [SerializeField] private TextMeshProUGUI textComponent;

        [Tooltip("描述框与被悬停 UI 的安全间距")]
        [SerializeField] private float padding = 15f;

        private GameObject _tooltipInstance;
        private RectTransform _tooltipRect;
        private RectTransform _currentOwner;

        private GameObject _loreTooltipInstance;
        private RectTransform _loreTooltipRect;
        private TextMeshProUGUI _loreTextComponent;
        private RectTransform _currentLoreOwner;

        public RectTransform CurrentOwner => _currentOwner;

        // Tooltip 专用独立 Canvas（ScreenSpaceOverlay），避免和主 Canvas 的 Camera 模式冲突
        private Canvas _overlayCanvas;
        private RectTransform _overlayRoot;

        private readonly System.Collections.Generic.List<GameObject> _activeTooltipInstances = new System.Collections.Generic.List<GameObject>();
        private readonly System.Collections.Generic.List<GameObject> _tooltipPool = new System.Collections.Generic.List<GameObject>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            CreateOverlayCanvas();
            InitializeTooltip();
        }

        private void CreateOverlayCanvas()
        {
            var go = new GameObject("TooltipOverlayCanvas");
            DontDestroyOnLoad(go);
            _overlayCanvas = go.AddComponent<Canvas>();
            _overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _overlayCanvas.sortingOrder = 200;
            _overlayRoot = go.GetComponent<RectTransform>();
        }

        private void InitializeTooltip()
        {
            if (tooltipPrefab == null)
            {
                Debug.LogError("[TooltipManager] 未在 Inspector 中指定 Tooltip Prefab.");
                return;
            }

            _tooltipInstance = Instantiate(tooltipPrefab, _overlayRoot);
            _tooltipRect = _tooltipInstance.GetComponent<RectTransform>();
            textComponent = _tooltipInstance.GetComponentInChildren<TextMeshProUGUI>();
            CleanupTooltipInstance(_tooltipInstance);
            _tooltipInstance.SetActive(false);
            _tooltipPool.Add(_tooltipInstance);

            _loreTooltipInstance = Instantiate(tooltipPrefab, _overlayRoot);
            _loreTooltipRect = _loreTooltipInstance.GetComponent<RectTransform>();
            _loreTextComponent = _loreTooltipInstance.GetComponentInChildren<TextMeshProUGUI>();
            CleanupTooltipInstance(_loreTooltipInstance);
            _loreTooltipInstance.SetActive(false);
            _tooltipPool.Add(_loreTooltipInstance);
        }

        private void CleanupTooltipInstance(GameObject go)
        {
            // 不需要嵌套 Canvas（已在独立的 Overlay Canvas 下）
            var c = go.GetComponent<Canvas>();
            if (c != null) Destroy(c);

            CanvasGroup cg = go.GetComponent<CanvasGroup>();
            if (cg == null) cg = go.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;

            RectTransform rectTrans = go.GetComponent<RectTransform>();
            if (rectTrans != null)
            {
                rectTrans.anchorMin = new Vector2(0.5f, 0.5f);
                rectTrans.anchorMax = new Vector2(0.5f, 0.5f);
                rectTrans.pivot = new Vector2(0.5f, 0.5f);
            }
        }

        private GameObject GetTooltipInstance()
        {
            GameObject instance = null;
            if (_tooltipPool.Count > 0)
            {
                instance = _tooltipPool[_tooltipPool.Count - 1];
                _tooltipPool.RemoveAt(_tooltipPool.Count - 1);
            }
            else
            {
                instance = Instantiate(tooltipPrefab, _overlayRoot);
                CleanupTooltipInstance(instance);
            }
            return instance;
        }

        private void HideAllActiveTooltips()
        {
            foreach (var instance in _activeTooltipInstances)
            {
                if (instance != null)
                {
                    instance.SetActive(false);
                    _tooltipPool.Add(instance);
                }
            }
            _activeTooltipInstances.Clear();
            _currentOwner = null;
            _currentLoreOwner = null;
        }

        public void ShowMultipleTooltips(RectTransform targetRect, System.Collections.Generic.List<string> contents)
        {
            if (targetRect == null || contents == null || contents.Count == 0 || tooltipPrefab == null) return;

            HideAllActiveTooltips();
            _currentOwner = targetRect;
            _currentLoreOwner = targetRect;

            System.Collections.Generic.List<RectTransform> tooltipRects = new System.Collections.Generic.List<RectTransform>();

            for (int i = 0; i < contents.Count; i++)
            {
                GameObject instance = GetTooltipInstance();
                var textComp = instance.GetComponentInChildren<TextMeshProUGUI>();
                if (textComp != null)
                {
                    textComp.text = contents[i];
                }

                instance.SetActive(true);
                _activeTooltipInstances.Add(instance);
                tooltipRects.Add(instance.GetComponent<RectTransform>());
            }

            Canvas.ForceUpdateCanvases();
            foreach (var rect in tooltipRects)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
            }

            // 用主 Canvas 的 Camera 转换目标坐标到屏幕空间
            Canvas mainCanvas = targetRect.GetComponentInParent<Canvas>();
            Camera uiCamera = (mainCanvas != null && mainCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
                ? mainCanvas.worldCamera : null;

            Vector3[] targetWorldCorners = new Vector3[4];
            targetRect.GetWorldCorners(targetWorldCorners);
            Vector2[] targetScreenCorners = new Vector2[4];
            for (int i = 0; i < 4; i++)
            {
                targetScreenCorners[i] = RectTransformUtility.WorldToScreenPoint(uiCamera, targetWorldCorners[i]);
            }
            Vector2 targetCenterScreen = (targetScreenCorners[0] + targetScreenCorners[2]) / 2f;

            Rect safeArea = AspectRatioController.Instance != null
                ? AspectRatioController.Instance.SafeArea
                : new Rect(0, 0, Screen.width, Screen.height);

            float leftSpace = targetScreenCorners[0].x - safeArea.xMin;
            float rightSpace = safeArea.xMax - targetScreenCorners[2].x;
            bool isRight = rightSpace >= leftSpace;

            System.Collections.Generic.List<System.Collections.Generic.List<RectTransform>> columns = new System.Collections.Generic.List<System.Collections.Generic.List<RectTransform>>();
            System.Collections.Generic.List<RectTransform> currentColumn = new System.Collections.Generic.List<RectTransform>();
            columns.Add(currentColumn);

            float currentColumnHeight = 0f;
            float maxVerticalSpace = safeArea.height - 2f * padding;

            for (int i = 0; i < tooltipRects.Count; i++)
            {
                var rect = tooltipRects[i];
                float h = rect.rect.height * rect.lossyScale.y;

                float heightNeeded = h;
                if (currentColumn.Count > 0) heightNeeded += padding;

                if (currentColumnHeight + heightNeeded > maxVerticalSpace && currentColumn.Count > 0)
                {
                    currentColumn = new System.Collections.Generic.List<RectTransform>();
                    columns.Add(currentColumn);
                    currentColumn.Add(rect);
                    currentColumnHeight = h;
                }
                else
                {
                    currentColumn.Add(rect);
                    currentColumnHeight += heightNeeded;
                }
            }

            float nextColStartX = isRight ? (targetScreenCorners[2].x + padding) : (targetScreenCorners[0].x - padding);

            for (int colIdx = 0; colIdx < columns.Count; colIdx++)
            {
                var colList = columns[colIdx];
                if (colList.Count == 0) continue;

                float colWidth = 0f;
                float colHeight = 0f;
                System.Collections.Generic.List<float> heights = new System.Collections.Generic.List<float>();

                for (int i = 0; i < colList.Count; i++)
                {
                    var rect = colList[i];
                    float w = rect.rect.width * rect.lossyScale.x;
                    float h = rect.rect.height * rect.lossyScale.y;
                    if (w > colWidth) colWidth = w;
                    colHeight += h;
                    if (i > 0) colHeight += padding;
                    heights.Add(h);
                }

                float colCenterX;
                if (isRight)
                {
                    colCenterX = nextColStartX + colWidth / 2f;
                    nextColStartX = nextColStartX + colWidth + padding;
                }
                else
                {
                    colCenterX = nextColStartX - colWidth / 2f;
                    nextColStartX = nextColStartX - colWidth - padding;
                }

                float minY = colHeight / 2f + padding + safeArea.yMin;
                float maxY = safeArea.yMax - colHeight / 2f - padding;
                float clampedCenterY = Mathf.Clamp(targetCenterScreen.y, minY, maxY);
                float startY = clampedCenterY + colHeight / 2f;

                float currentY = startY;
                for (int i = 0; i < colList.Count; i++)
                {
                    var rect = colList[i];
                    float h = heights[i];

                    float itemCenterY = currentY - h / 2f;
                    currentY -= (h + padding);

                    float finalX = Mathf.Clamp(colCenterX, safeArea.xMin + colWidth / 2f + padding, safeArea.xMax - colWidth / 2f - padding);
                    float finalY = Mathf.Clamp(itemCenterY, safeArea.yMin + h / 2f + padding, safeArea.yMax - h / 2f - padding);

                    // Tooltip 在独立的 ScreenSpaceOverlay Canvas 上，直接用屏幕坐标
                    rect.position = new Vector3(finalX, finalY, 0f);
                }
            }
        }

        public void ShowTooltip(string content, RectTransform targetRect)
        {
            ShowMultipleTooltips(targetRect, new System.Collections.Generic.List<string> { content });
        }

        public void HideTooltip()
        {
            HideAllActiveTooltips();
        }

        public void HideTooltip(RectTransform targetRect)
        {
            if (_currentOwner == targetRect)
            {
                HideAllActiveTooltips();
            }
        }

        public void ShowCardEffectsTooltip(CGM.Data.CardInfo cardInfo, RectTransform targetRect)
        {
            string statusText = GetCardEffectsTooltipText(cardInfo);
            if (!string.IsNullOrEmpty(statusText))
            {
                ShowTooltip(statusText, targetRect);
            }
        }

        public string GetCardEffectsTooltipText(CGM.Data.CardInfo cardInfo)
        {
            if (cardInfo == null || cardInfo.effects == null) return "";

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            int count = 0;

            foreach (var effect in cardInfo.effects)
            {
                if (effect.effectType == "apply_buff" || effect.effectType == "apply_debuff")
                {
                    try
                    {
                        CGM.Data.BuffId buffId = effect.GetBuffId();
                        var buffInfo = CGM.Data.BuffDatabase.Get(buffId);
                        if (buffInfo != null)
                        {
                            if (count > 0) sb.Append("\n\n");
                            sb.Append($"<color={buffInfo.colorHex}><b>{buffInfo.name}</b></color>\n{buffInfo.description}");
                            count++;
                        }
                    }
                    catch (System.Exception) { }
                }
            }

            return sb.ToString();
        }

        public void ShowLoreTooltip(string content, RectTransform targetRect)
        {
            ShowMultipleTooltips(targetRect, new System.Collections.Generic.List<string> { content });
        }

        public void ShowDualTooltips(string loreContent, string statusContent, RectTransform targetRect)
        {
            ShowMultipleTooltips(targetRect, new System.Collections.Generic.List<string> { loreContent, statusContent });
        }

        public void HideLoreTooltip()
        {
            if (_currentLoreOwner != null)
            {
                HideAllActiveTooltips();
            }
        }

        public void HideLoreTooltip(RectTransform targetRect)
        {
            if (_currentLoreOwner == targetRect)
            {
                HideAllActiveTooltips();
            }
        }
    }
}
