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

            // 实例化描述框为 TooltipManager 的子物体
            _tooltipInstance = Instantiate(tooltipPrefab, transform);
            _tooltipRect = _tooltipInstance.GetComponent<RectTransform>();

            // 自动配置 Canvas 组件，以保证描述框始终叠在最前层（高于卡牌 Hover 时的层级 30/35）
            Canvas canvas = _tooltipInstance.GetComponent<Canvas>();
            if (canvas == null)
            {
                canvas = _tooltipInstance.AddComponent<Canvas>();
            }
            canvas.overrideSorting = true;
            canvas.sortingOrder = 100;

            // 自动检索文本组件（如果未在 Inspector 中显式指定）
            if (textComponent == null)
            {
                textComponent = _tooltipInstance.GetComponentInChildren<TextMeshProUGUI>();
                if (textComponent == null)
                {
                    Debug.LogError("[TooltipManager] 无法在 Tooltip Prefab 的子节点中找到 TextMeshProUGUI 组件.");
                }
            }

            // 强行重置锚点和 Pivot 为 (0.5, 0.5) 居中对齐，方便定位数学运算
            if (_tooltipRect != null)
            {
                _tooltipRect.anchorMin = new Vector2(0.5f, 0.5f);
                _tooltipRect.anchorMax = new Vector2(0.5f, 0.5f);
                _tooltipRect.pivot = new Vector2(0.5f, 0.5f);
            }

            _tooltipInstance.SetActive(false);
        }

        /// <summary>
        /// 显示描述框，自动计算侧边防遮挡定位和屏幕边缘 Clamp。
        /// </summary>
        /// <param name="content">填入描述框的富文本内容</param>
        /// <param name="targetRect">被悬停 UI 物体的 RectTransform</param>
        public void ShowTooltip(string content, RectTransform targetRect)
        {
            // 强制将 TooltipManager 移到 Canvas 的最前方渲染
            transform.SetAsLastSibling();

            if (_tooltipInstance == null || textComponent == null || targetRect == null)
            {
                return;
            }

            // 填充文字并激活
            textComponent.text = content;
            _tooltipInstance.SetActive(true);

            // 强制重新计算排版，使 Content Size Fitter 立即计算出真实的长宽
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_tooltipRect);

            // 获取 Canvas 以及对应的渲染 Camera（Screen Space Camera 模式下必不可少）
            Canvas canvas = targetRect.GetComponentInParent<Canvas>();
            Camera uiCamera = null;
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                uiCamera = canvas.worldCamera;
            }

            // 获取悬停目标的四个世界坐标点
            Vector3[] targetWorldCorners = new Vector3[4];
            targetRect.GetWorldCorners(targetWorldCorners);

            // 将目标的四个点转换为统一的 Screen Point（像素空间）
            Vector2[] targetScreenCorners = new Vector2[4];
            for (int i = 0; i < 4; i++)
            {
                targetScreenCorners[i] = RectTransformUtility.WorldToScreenPoint(uiCamera, targetWorldCorners[i]);
            }

            // 目标物体的中心点（像素空间）
            Vector2 targetCenterScreen = (targetScreenCorners[0] + targetScreenCorners[2]) / 2f;

            // 计算描述框实际大小（像素空间）
            float tooltipWidth = _tooltipRect.rect.width * _tooltipRect.lossyScale.x;
            float tooltipHeight = _tooltipRect.rect.height * _tooltipRect.lossyScale.y;

            // 默认位置：放置在目标的右侧（像素空间）
            Vector2 screenPos = targetCenterScreen;
            screenPos.x = targetScreenCorners[2].x + (tooltipWidth / 2f) + padding;

            // 检测右侧是否溢出屏幕
            if (screenPos.x + (tooltipWidth / 2f) > Screen.width)
            {
                // 如果右侧放不下，翻转到目标的左侧
                screenPos.x = targetScreenCorners[0].x - (tooltipWidth / 2f) - padding;
            }

            // 垂直方向限制：防止描述框顶部或底部飞出屏幕边缘
            float minY = (tooltipHeight / 2f) + padding;
            float maxY = Screen.height - (tooltipHeight / 2f) - padding;
            screenPos.y = Mathf.Clamp(screenPos.y, minY, maxY);

            // 水平方向保底限制：绝对不允许描述框超出屏幕边界
            float minX = (tooltipWidth / 2f) + padding;
            float maxX = Screen.width - (tooltipWidth / 2f) - padding;
            screenPos.x = Mathf.Clamp(screenPos.x, minX, maxX);

            // 将计算好的 Screen Pos 转换回 World Pos 赋值给描述框的 Transform
            Vector3 worldPos;
            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(_tooltipRect, screenPos, uiCamera, out worldPos))
            {
                _tooltipRect.position = worldPos;
            }
        }

        /// <summary>
        /// 关闭/隐藏描述框
        /// </summary>
        public void HideTooltip()
        {
            if (_tooltipInstance != null)
            {
                _tooltipInstance.SetActive(false);
            }
        }

        /// <summary>
        /// 显示卡牌所含 Buff/Debuff 的描述框悬停提示。
        /// </summary>
        /// <param name="cardInfo">卡牌数据</param>
        /// <param name="targetRect">被悬停 UI 物体的 RectTransform</param>
        public void ShowCardEffectsTooltip(CGM.Data.CardInfo cardInfo, RectTransform targetRect)
        {
            if (cardInfo == null || cardInfo.effects == null) return;

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

            if (count > 0)
            {
                ShowTooltip(sb.ToString(), targetRect);
            }
        }
    }
}
