using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CGM.UI
{
    /// <summary>
    /// 全局通用自适应 Tooltip (描述框) 管理器。
    /// 挂载在 UI Canvas 下的任何 GameObject 上，并拖入 CustomTooltip 预制体引用。
    /// </summary>
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
        private RectTransform _currentOwner; // 当前拥有/显示该描述框的 UI 物体

        private GameObject _loreTooltipInstance;
        private RectTransform _loreTooltipRect;
        private TextMeshProUGUI _loreTextComponent;
        private RectTransform _currentLoreOwner;

        // 公开当前拥有者属性
        public RectTransform CurrentOwner => _currentOwner;

        // 描述框对象池与活跃实例列表
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

            InitializeTooltip();
        }

        private void InitializeTooltip()
        {
            if (tooltipPrefab == null)
            {
                Debug.LogError("[TooltipManager] 未在 Inspector 中指定 Tooltip Prefab.");
                return;
            }

            // 1. 实例化第一个描述框，配置并加入池中
            _tooltipInstance = Instantiate(tooltipPrefab, transform);
            _tooltipRect = _tooltipInstance.GetComponent<RectTransform>();
            textComponent = _tooltipInstance.GetComponentInChildren<TextMeshProUGUI>();
            ConfigureCanvasAndCanvasGroup(_tooltipInstance);
            _tooltipInstance.SetActive(false);
            _tooltipPool.Add(_tooltipInstance);

            // 2. 实例化第二个描述框 (卡牌 Lore 等使用)，配置并加入池中
            _loreTooltipInstance = Instantiate(tooltipPrefab, transform);
            _loreTooltipRect = _loreTooltipInstance.GetComponent<RectTransform>();
            _loreTextComponent = _loreTooltipInstance.GetComponentInChildren<TextMeshProUGUI>();
            ConfigureCanvasAndCanvasGroup(_loreTooltipInstance);
            _loreTooltipInstance.SetActive(false);
            _tooltipPool.Add(_loreTooltipInstance);
        }

        private void ConfigureCanvasAndCanvasGroup(GameObject go)
        {
            Canvas canvas = go.GetComponent<Canvas>();
            if (canvas == null) canvas = go.AddComponent<Canvas>();

            // 匹配主 Canvas 的渲染模式，避免嵌套 Canvas 坐标系错位
            Canvas mainCanvas = go.transform.parent != null
                ? go.transform.parent.GetComponentInParent<Canvas>()
                : null;
            if (mainCanvas != null && mainCanvas.renderMode == RenderMode.ScreenSpaceCamera)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = mainCanvas.worldCamera;
                canvas.planeDistance = mainCanvas.planeDistance;
            }

            canvas.overrideSorting = true;
            canvas.sortingOrder = 100;

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
                instance = Instantiate(tooltipPrefab, transform);
                ConfigureCanvasAndCanvasGroup(instance);
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

        /// <summary>
        /// 统一展现多个提示描述框，支持纵向同侧排布 + 空间不足自动横向开新列（折行）排布。
        /// </summary>
        public void ShowMultipleTooltips(RectTransform targetRect, System.Collections.Generic.List<string> contents)
        {
            if (targetRect == null || contents == null || contents.Count == 0 || tooltipPrefab == null) return;

            // 1. 先回收当前所有的活跃提示框
            HideAllActiveTooltips();

            // 记录当前的拥有者
            _currentOwner = targetRect;
            _currentLoreOwner = targetRect;

            // 强制将 TooltipManager 移到 Canvas 的最前方渲染
            transform.SetAsLastSibling();

            System.Collections.Generic.List<RectTransform> tooltipRects = new System.Collections.Generic.List<RectTransform>();

            // 2. 从池中获取并配置实例
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

            // 3. 强制刷新布局以计算真实的宽高
            Canvas.ForceUpdateCanvases();
            foreach (var rect in tooltipRects)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
            }

            // 4. 获取 Canvas 与摄像机
            Canvas canvas = targetRect.GetComponentInParent<Canvas>();
            Camera uiCamera = null;
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                uiCamera = canvas.worldCamera;
            }

            // 5. 获取悬停目标的屏幕四角
            Vector3[] targetWorldCorners = new Vector3[4];
            targetRect.GetWorldCorners(targetWorldCorners);
            Vector2[] targetScreenCorners = new Vector2[4];
            for (int i = 0; i < 4; i++)
            {
                targetScreenCorners[i] = RectTransformUtility.WorldToScreenPoint(uiCamera, targetWorldCorners[i]);
            }
            Vector2 targetCenterScreen = (targetScreenCorners[0] + targetScreenCorners[2]) / 2f;

            // 6. 获取实际 UI 渲染安全区域（16:9 黑边适配后可见范围可能小于全屏）
            Rect safeArea = AspectRatioController.Instance != null
                ? AspectRatioController.Instance.SafeArea
                : new Rect(0, 0, Screen.width, Screen.height);

            // 7. 确定放置在左侧还是右侧（优先右侧）
            float leftSpace = targetScreenCorners[0].x - safeArea.xMin;
            float rightSpace = safeArea.xMax - targetScreenCorners[2].x;
            bool isRight = rightSpace >= leftSpace;

            // 8. 进行分列排版（纵向空间限制）
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
                if (currentColumn.Count > 0)
                {
                    heightNeeded += padding;
                }

                if (currentColumnHeight + heightNeeded > maxVerticalSpace && currentColumn.Count > 0)
                {
                    // 超出了本列最大高度，新开一列
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

            // 8. 依次放置每一列
            float nextColStartX = isRight ? (targetScreenCorners[2].x + padding) : (targetScreenCorners[0].x - padding);

            for (int colIdx = 0; colIdx < columns.Count; colIdx++)
            {
                var colList = columns[colIdx];
                if (colList.Count == 0) continue;

                // 计算这一列的最大宽度与总高度
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

                // 确定这一列的 X 坐标中心
                float colCenterX;
                if (isRight)
                {
                    colCenterX = nextColStartX + colWidth / 2f;
                    nextColStartX = nextColStartX + colWidth + padding; // 下一列向右偏移
                }
                else
                {
                    colCenterX = nextColStartX - colWidth / 2f;
                    nextColStartX = nextColStartX - colWidth - padding; // 下一列向左偏移
                }

                // 限制 Y 轴堆叠不越出安全区域顶部和底部
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

                    // Clamp 限制在安全区域内
                    float finalX = Mathf.Clamp(colCenterX, safeArea.xMin + colWidth / 2f + padding, safeArea.xMax - colWidth / 2f - padding);
                    float finalY = Mathf.Clamp(itemCenterY, safeArea.yMin + h / 2f + padding, safeArea.yMax - h / 2f - padding);

                    Vector2 screenPos = new Vector2(finalX, finalY);
                    Vector3 worldPos;
                    if (RectTransformUtility.ScreenPointToWorldPointInRectangle(rect, screenPos, uiCamera, out worldPos))
                    {
                        rect.position = worldPos;
                    }
                }
            }
        }

        /// <summary>
        /// 显示描述框，自动计算侧边防遮挡定位和屏幕边缘 Clamp。
        /// </summary>
        public void ShowTooltip(string content, RectTransform targetRect)
        {
            ShowMultipleTooltips(targetRect, new System.Collections.Generic.List<string> { content });
        }

        /// <summary>
        /// 强制关闭/隐藏描述框（清除拥有者关系）
        /// </summary>
        public void HideTooltip()
        {
            HideAllActiveTooltips();
        }

        /// <summary>
        /// 仅当指定的悬停目标是当前描述框的拥有者时，才关闭/隐藏描述框
        /// </summary>
        public void HideTooltip(RectTransform targetRect)
        {
            if (_currentOwner == targetRect)
            {
                HideAllActiveTooltips();
            }
        }

        /// <summary>
        /// 显示卡牌所含 Buff/Debuff 的描述框悬停提示。
        /// </summary>
        public void ShowCardEffectsTooltip(CGM.Data.CardInfo cardInfo, RectTransform targetRect)
        {
            string statusText = GetCardEffectsTooltipText(cardInfo);
            if (!string.IsNullOrEmpty(statusText))
            {
                ShowTooltip(statusText, targetRect);
            }
        }

        /// <summary>
        /// 获取卡牌所含 Buff/Debuff 的描述框富文本内容。
        /// </summary>
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

        /// <summary>
        /// 仅显示卡牌的 Lore/介绍描述框。
        /// </summary>
        public void ShowLoreTooltip(string content, RectTransform targetRect)
        {
            ShowMultipleTooltips(targetRect, new System.Collections.Generic.List<string> { content });
        }

        /// <summary>
        /// 同时显示卡牌的 Lore/介绍描述框 与 Buff状态 描述框，并智能排版。
        /// </summary>
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
