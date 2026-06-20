// =============================================================================
// EndingPanelController.cs — 结束界面控制器
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CGM.Core;

namespace CGM.UI
{
    public class EndingPanelController : MonoBehaviour
    {
        [Header("胜负标题")]
        [SerializeField] private GameObject successUI;
        [SerializeField] private GameObject failureUI;

        [Header("数据容器")]
        [SerializeField] private Transform contentContainer;
        [SerializeField] private GameObject dataTextPrefab;

        [Header("返回按钮")]
        [SerializeField] private Button returnButton;

        private void Awake()
        {
            AutoResolveReferences();
        }

        private void Start()
        {
            if (returnButton != null)
                returnButton.onClick.AddListener(OnReturnClicked);
        }

        /// <summary>
        /// 显示胜利结算。
        /// </summary>
        public void ShowVictory(List<StatLine> stats)
        {
            if (successUI != null) successUI.SetActive(true);
            if (failureUI != null) failureUI.SetActive(false);
            PopulateStats(stats);
        }

        /// <summary>
        /// 显示失败结算。
        /// </summary>
        public void ShowDefeat(List<StatLine> stats)
        {
            if (successUI != null) successUI.SetActive(false);
            if (failureUI != null) failureUI.SetActive(true);
            PopulateStats(stats);
        }

        private void SetupScrollRect()
        {
            Transform scrollViewTrans = transform.Find("Stats_ScrollView");
            if (scrollViewTrans == null) return;

            // 1. 确保 ScrollRect 存在于 Stats_ScrollView 上
            ScrollRect scrollRect = scrollViewTrans.GetComponent<ScrollRect>();
            if (scrollRect == null)
            {
                scrollRect = scrollViewTrans.gameObject.AddComponent<ScrollRect>();
                scrollRect.horizontal = false;
                scrollRect.vertical = true;
            }

            // 设置滚动灵敏度，使鼠标滚轮滑动更加流畅快速
            scrollRect.scrollSensitivity = 75.0f;

            // 2. 确保 Viewport 遮罩正常设置，裁剪溢出内容
            Transform viewport = scrollViewTrans.Find("Viewport");
            if (viewport != null)
            {
                scrollRect.viewport = viewport.GetComponent<RectTransform>();
                if (viewport.GetComponent<Mask>() == null && viewport.GetComponent<RectMask2D>() == null)
                {
                    viewport.gameObject.AddComponent<RectMask2D>();
                }
            }

            // 3. 确保 Content 容器配置了 Layout 与 SizeFitter，支持动态高度增长和宽度拉伸
            if (contentContainer != null)
            {
                scrollRect.content = contentContainer.GetComponent<RectTransform>();

                var layout = contentContainer.GetComponent<VerticalLayoutGroup>();
                if (layout == null)
                {
                    layout = contentContainer.gameObject.AddComponent<VerticalLayoutGroup>();
                }
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = false;
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.padding = new RectOffset(20, 20, 10, 10);
                layout.spacing = 8f;

                var fitter = contentContainer.GetComponent<ContentSizeFitter>();
                if (fitter == null)
                {
                    fitter = contentContainer.gameObject.AddComponent<ContentSizeFitter>();
                }
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

                var rectTrans = contentContainer.GetComponent<RectTransform>();
                if (rectTrans != null)
                {
                    rectTrans.anchorMin = new Vector2(0f, 1f);
                    rectTrans.anchorMax = new Vector2(1f, 1f);
                    rectTrans.pivot = new Vector2(0.5f, 1f);
                    rectTrans.offsetMin = new Vector2(0f, rectTrans.offsetMin.y);
                    rectTrans.offsetMax = new Vector2(0f, rectTrans.offsetMax.y);
                }
            }
        }

        private void PopulateStats(List<StatLine> stats)
        {
            SetupScrollRect(); // 动态初始化并修补滑动与排版组件，保证布局自适应与滚轮正常工作
            if (contentContainer == null || dataTextPrefab == null) return;

            // 清空旧数据
            foreach (Transform child in contentContainer)
                Destroy(child.gameObject);

            if (stats == null) return;

            string currentCategory = "";

            foreach (var line in stats)
            {
                if (string.IsNullOrEmpty(line.value))
                {
                    // 检测并记录当前所处的分类，以便后续行统一左右颜色配置
                    if (line.label.Contains("战役结果")) currentCategory = "Result";
                    else if (line.label.Contains("战斗统计")) currentCategory = "Combat";
                    else if (line.label.Contains("牌组统计")) currentCategory = "Deck";

                    // 分类小标题或空行：居中对齐，并使用颜色上色高亮显示
                    var go = Instantiate(dataTextPrefab, contentContainer);
                    var txt = go.GetComponent<TextMeshProUGUI>();
                    if (txt == null)
                        txt = go.GetComponentInChildren<TextMeshProUGUI>();

                    if (txt != null)
                    {
                        txt.richText = true;
                        txt.alignment = TextAlignmentOptions.Midline;
                        if (!string.IsNullOrEmpty(line.colorHex))
                        {
                            txt.text = $"<color={line.colorHex}>{line.label}</color>";
                        }
                        else
                        {
                            txt.text = line.label;
                        }
                    }
                }
                else
                {
                    // 数据明细行：创建一个包含左右两个独立文本的行容器，解决对齐与分辨率适配问题
                    var rowGo = new GameObject("StatRow", typeof(RectTransform));
                    rowGo.transform.SetParent(contentContainer, false);
                    
                    var rowRect = rowGo.GetComponent<RectTransform>();
                    rowRect.sizeDelta = new Vector2(800f, 50f); // 预设高度

                    // 核心修复：必须加装 LayoutElement 并指明高度，否则 VerticalLayoutGroup 会将其算作高度 0 导致重叠堆叠混乱
                    var element = rowGo.AddComponent<LayoutElement>();
                    element.preferredHeight = 50f;
                    element.minHeight = 50f;

                    // 核心优化：按分类统一左右侧颜色，保持视觉美观一致
                    string labelColor = "#FFFFFF";
                    string valueColor = "#FFFFFF";
                    if (currentCategory == "Result")
                    {
                        labelColor = "#DCDCDC"; // 软浅灰色
                        valueColor = line.colorHex; // 战役结果使用原有红/绿色高亮
                    }
                    else if (currentCategory == "Combat")
                    {
                        labelColor = "#B0ECE0"; // 软浅绿色
                        valueColor = "#FFD700"; // 统一金色数字
                    }
                    else if (currentCategory == "Deck")
                    {
                        labelColor = "#FFEAA0"; // 软浅黄色
                        valueColor = "#52D0FF"; // 统一明亮蓝色数字
                    }

                    // 创建左侧 Label 文本 (占 70% 宽度，靠左)
                    var labelGo = Instantiate(dataTextPrefab, rowGo.transform);
                    labelGo.name = "Label";
                    var labelRect = labelGo.GetComponent<RectTransform>();
                    labelRect.anchorMin = new Vector2(0f, 0f);
                    labelRect.anchorMax = new Vector2(0.7f, 1f);
                    labelRect.pivot = new Vector2(0f, 0.5f);
                    labelRect.offsetMin = new Vector2(20f, 0f); // 靠左侧留出间距
                    labelRect.offsetMax = new Vector2(0f, 0f);

                    var labelTxt = labelGo.GetComponent<TextMeshProUGUI>();
                    if (labelTxt == null)
                        labelTxt = labelGo.GetComponentInChildren<TextMeshProUGUI>();
                    if (labelTxt != null)
                    {
                        labelTxt.alignment = TextAlignmentOptions.MidlineLeft;
                        labelTxt.text = $"<color={labelColor}>{line.label}</color>";
                    }

                    // 创建右侧 Value 文本 (占 30% 宽度，靠右)
                    var valueGo = Instantiate(dataTextPrefab, rowGo.transform);
                    valueGo.name = "Value";
                    var valueRect = valueGo.GetComponent<RectTransform>();
                    valueRect.anchorMin = new Vector2(0.7f, 0f);
                    valueRect.anchorMax = new Vector2(1f, 1f);
                    valueRect.pivot = new Vector2(1f, 0.5f);
                    valueRect.offsetMin = new Vector2(0f, 0f);
                    valueRect.offsetMax = new Vector2(-20f, 0f); // 靠右侧留出间距

                    var valueTxt = valueGo.GetComponent<TextMeshProUGUI>();
                    if (valueTxt == null)
                        valueTxt = valueGo.GetComponentInChildren<TextMeshProUGUI>();
                    if (valueTxt != null)
                    {
                        valueTxt.alignment = TextAlignmentOptions.MidlineRight;
                        valueTxt.text = $"<color={valueColor}><b>{line.value}</b></color>";
                    }
                }
            }

            // 核心功能：重置滚动条位置，使面板初始显示在最顶端
            Transform scrollViewTrans = transform.Find("Stats_ScrollView");
            if (scrollViewTrans != null)
            {
                ScrollRect scrollRect = scrollViewTrans.GetComponent<ScrollRect>();
                if (scrollRect != null)
                {
                    Canvas.ForceUpdateCanvases();
                    scrollRect.verticalNormalizedPosition = 1f;
                }
            }
        }

        private void OnReturnClicked()
        {
            var gsm = GameSessionManager.Instance;
            if (gsm != null) gsm.ReturnToMainMenu();
        }

        private void AutoResolveReferences()
        {
            if (successUI == null)   { var t = transform.Find("ResultTitleUI/SuccessUI"); if (t) successUI = t.gameObject; }
            if (failureUI == null)   { var t = transform.Find("ResultTitleUI/FaliureUI"); if (t) failureUI = t.gameObject; }
            if (contentContainer == null) { var t = transform.Find("Stats_ScrollView/Viewport/Content"); if (t) contentContainer = t; }
            if (returnButton == null) { var t = transform.Find("RestartGame"); if (t) returnButton = t.GetComponent<Button>(); }
            if (dataTextPrefab == null) dataTextPrefab = Resources.Load<GameObject>("Prefabs/DataText");
        }
    }

    /// <summary>
    /// 一条结算数据。
    /// </summary>
    public struct StatLine
    {
        public string label;    // 名称，如 "击败敌人"
        public string value;    // 内容，如 "6"
        public string colorHex; // 标签颜色，如 "#FFAD1F"
    }
}
