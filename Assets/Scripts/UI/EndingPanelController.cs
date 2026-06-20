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

        private void PopulateStats(List<StatLine> stats)
        {
            if (contentContainer == null || dataTextPrefab == null) return;

            // 清空旧数据
            foreach (Transform child in contentContainer)
                Destroy(child.gameObject);

            if (stats == null) return;

            foreach (var line in stats)
            {
                var go = Instantiate(dataTextPrefab, contentContainer);
                var txt = go.GetComponent<TextMeshProUGUI>();
                if (txt == null)
                    txt = go.GetComponentInChildren<TextMeshProUGUI>();

                if (txt != null)
                {
                    txt.richText = true;
                    // 强制对齐为 Left，使 <align=right> 标记能将数值推到右边界分栏对齐
                    txt.alignment = TextAlignmentOptions.Left;

                    if (string.IsNullOrEmpty(line.value))
                    {
                        // 这是一个分类小标题或空行
                        txt.text = line.label;
                    }
                    else
                    {
                        // 标签靠左，数值靠右，高亮颜色并且加粗显示
                        txt.text = $"{line.label} <align=right><color={line.colorHex}><b>{line.value}</b></color></align>";
                    }
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
