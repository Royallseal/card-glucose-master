using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CGM.Core;

namespace CGM.UI
{
    /// <summary>
    /// 顶部栏 (Ultop) 控制器，实时渲染血量、血糖、金币、关卡状态和牌组数量。
    /// </summary>
    public class UltopController : MonoBehaviour
    {
        [Header("属性值 UI 引用")]
        [Tooltip("角色血量文本 (HP_Value)")]
        [SerializeField] private TextMeshProUGUI hpValueText;
        [Tooltip("当前血糖文本 (CGM_Value)")]
        [SerializeField] private TextMeshProUGUI cgmValueText;
        [Tooltip("金币数量文本 (Gold_Value)")]
        [SerializeField] private TextMeshProUGUI goldValueText;

        [Header("关卡 UI 引用")]
        [Tooltip("当前关卡文本 (Current_Levle_Value)")]
        [SerializeField] private TextMeshProUGUI currentLevelText;
        [Tooltip("下一关关卡文本 (Next_Levle_Value)")]
        [SerializeField] private TextMeshProUGUI nextLevelText;

        [Header("卡组 UI 引用")]
        [Tooltip("卡组数量文本 (Text (TMP))")]
        [SerializeField] private TextMeshProUGUI cardsCountText;

        [Header("核心系统引用")]
        [SerializeField] private PlayerStats playerStats;
        [SerializeField] private BattleSessionController battleController;

        [Header("设置面板引用")]
        [SerializeField] private Button settingButton;
        [SerializeField] private GameObject settingPanel;

        private void Start()
        {
            // 自动解析依赖
            ResolveDependencies();

            // 绑定事件监听（playerStats 可能为 null，由 GameSessionManager 后续注入）
            BindPlayerStatsEvents();

            if (battleController != null)
            {
                battleController.OnPilesChanged += UpdateCardsCount;
                battleController.OnPhaseChanged += UpdateCardsCountOnPhaseChange;
            }

            if (LevelManager.Instance != null)
            {
                LevelManager.Instance.OnLevelChanged += UpdateLevelUI;
            }

            if (settingButton != null && settingPanel != null)
            {
                settingButton.onClick.AddListener(ToggleSettingPanel);

                // 统一配置设置按钮的 Hover 特效和音效
                var settingHover = settingButton.gameObject.GetComponent<UIHoverButtonEffects>();
                if (settingHover == null) settingHover = settingButton.gameObject.AddComponent<UIHoverButtonEffects>();
                settingHover.Setup(Resources.Load<AudioClip>("Audio/Button_Hover"), 1.1f);
            }

            // 统一配置卡组按钮的 Hover 特效和音效
            Transform cardsButtonTrans = transform.Find("Icon_Line/Cards");
            if (cardsButtonTrans != null)
            {
                var cardsHover = cardsButtonTrans.gameObject.GetComponent<UIHoverButtonEffects>();
                if (cardsHover == null) cardsHover = cardsButtonTrans.gameObject.AddComponent<UIHoverButtonEffects>();
                cardsHover.Setup(Resources.Load<AudioClip>("Audio/Button_Hover"), 1.05f);
            }

            // 初始化 UI 显示
            UpdateAllUI();
        }

        private void OnDestroy()
        {
            if (playerStats != null)
            {
                playerStats.OnStatsChanged -= UpdateHpUI;
                playerStats.OnGlucoseChanged -= UpdateGlucoseUI;
                playerStats.OnGoldChanged -= UpdateGoldUI;
            }

            if (battleController != null)
            {
                battleController.OnPilesChanged -= UpdateCardsCount;
                battleController.OnPhaseChanged -= UpdateCardsCountOnPhaseChange;
            }

            if (LevelManager.Instance != null)
            {
                LevelManager.Instance.OnLevelChanged -= UpdateLevelUI;
            }

            if (settingButton != null)
            {
                settingButton.onClick.RemoveListener(ToggleSettingPanel);
            }
        }

        /// <summary>
        /// 全局更新所有 UI 文本
        /// </summary>
        public void UpdateAllUI()
        {
            UpdateHpUI();
            UpdateGlucoseUI(playerStats != null ? playerStats.CurrentGlucose : 5.7f);
            UpdateGoldUI(playerStats != null ? playerStats.Gold : 99);
            UpdateLevelUI();
            UpdateCardsCount();
        }

        /// <summary>
        /// 由 GameSessionManager 注入 PlayerStats（避免 FindObjectOfType 查 inactive 对象失败）。
        /// </summary>
        public void SetPlayerStats(PlayerStats stats)
        {
            if (playerStats != null)
            {
                // 解绑旧引用
                playerStats.OnStatsChanged -= UpdateHpUI;
                playerStats.OnGlucoseChanged -= UpdateGlucoseUI;
                playerStats.OnGoldChanged -= UpdateGoldUI;
            }
            playerStats = stats;
            BindPlayerStatsEvents();
            UpdateAllUI();
        }

        private void BindPlayerStatsEvents()
        {
            if (playerStats != null)
            {
                playerStats.OnStatsChanged -= UpdateHpUI;
                playerStats.OnGlucoseChanged -= UpdateGlucoseUI;
                playerStats.OnGoldChanged -= UpdateGoldUI;
                playerStats.OnStatsChanged += UpdateHpUI;
                playerStats.OnGlucoseChanged += UpdateGlucoseUI;
                playerStats.OnGoldChanged += UpdateGoldUI;
            }
        }

        private void ResolveDependencies()
        {
            if (playerStats == null)
            {
                playerStats = FindObjectOfType<PlayerStats>();
            }

            if (battleController == null)
            {
                battleController = FindObjectOfType<BattleSessionController>();
            }
        }

        private void UpdateHpUI()
        {
            if (hpValueText == null) return;

            if (playerStats != null)
            {
                // 血量实时更新，字体全段设置为红色，使用 BattleConstants.ColorRed 富文本
                hpValueText.text = $"<color={BattleConstants.ColorRed}>{playerStats.CurrentHp}/{playerStats.MaxHp}</color>";
            }
            else
            {
                hpValueText.text = "-/-";
            }
        }

        private void UpdateGlucoseUI(float glucose)
        {
            if (cgmValueText == null) return;

            string stateName = GetGlucoseStateName(glucose);
            string colorHex = GetGlucoseStateColorHex(glucose);

            // 整块文字变色，血糖值保留一位小数
            cgmValueText.text = $"<color={colorHex}>{glucose:F1}·{stateName}</color>";
        }

        private void UpdateGoldUI(int gold)
        {
            if (goldValueText == null) return;

            // 金币数随玩家金币实时变化，文字颜色改为黄色 ColorGold
            goldValueText.text = $"<color={BattleConstants.ColorGold}>{gold}</color>";
        }

        private void UpdateLevelUI()
        {
            if (currentLevelText == null || nextLevelText == null) return;

            if (LevelManager.Instance != null)
            {
                var current = LevelManager.Instance.CurrentNode;
                var next = LevelManager.Instance.NextNode;

                if (current != null)
                {
                    // 关卡数与关卡名不同颜色
                    currentLevelText.text = $"关卡 {current.number}: <color={BattleConstants.ColorOrange}>{current.levelName}</color>";
                }
                else
                {
                    currentLevelText.text = "关卡: -";
                }

                if (next != null)
                {
                    // 下一关文本一样，但是颜色较淡 (深灰色 #555555 以防看不清)
                    nextLevelText.text = $"下一关: <color=#555555>{next.levelName}</color>";
                }
                else
                {
                    nextLevelText.text = "下一关: <color=#555555>无</color>";
                }
            }
        }

        private void UpdateCardsCount(IReadOnlyList<CGM.Data.CardInfo> hand, IReadOnlyList<CGM.Data.CardInfo> draw, IReadOnlyList<CGM.Data.CardInfo> discard)
        {
            UpdateCardsCount();
        }

        private void UpdateCardsCountOnPhaseChange(BattleTurnPhase phase)
        {
            UpdateCardsCount();
        }

        public void UpdateCardsCount()
        {
            if (cardsCountText == null) return;

            int totalCards = 0;
            if (battleController != null && battleController.StartingDeckCardIds != null)
            {
                totalCards = battleController.StartingDeckCardIds.Count;
            }

            cardsCountText.text = totalCards.ToString();
        }

        private void ToggleSettingPanel()
        {
            if (settingPanel != null)
            {
                settingPanel.SetActive(!settingPanel.activeSelf);
            }
        }

        private string GetGlucoseStateName(float glucose)
        {
            if (glucose < BattleConstants.HealthyGlucoseMin)
            {
                return "低血糖";
            }
            else if (glucose <= BattleConstants.HealthyGlucoseMax)
            {
                return "健康";
            }
            else
            {
                return "高血糖";
            }
        }

        private string GetGlucoseStateColorHex(float glucose)
        {
            if (glucose < BattleConstants.HealthyGlucoseMin)
            {
                return BattleConstants.ColorOrange; // 低血糖橙黄色
            }
            else if (glucose <= BattleConstants.HealthyGlucoseMax)
            {
                return BattleConstants.ColorGreen;  // 健康绿色
            }
            else
            {
                return BattleConstants.ColorRed;    // 高血糖红色
            }
        }

#if UNITY_EDITOR
        private void Reset()
        {
            // 在编辑器下拖入或重置脚本时，自动按层级名称查找到对应的 UI 控件引用，减免手动拖曳工作量
            Transform iconLine = transform.Find("Icon_Line");
            if (iconLine != null)
            {
                Transform hpTrans = iconLine.Find("HP/HP_Value");
                if (hpTrans != null) hpValueText = hpTrans.GetComponent<TMPro.TextMeshProUGUI>();

                Transform cgmTrans = iconLine.Find("CGM/CGM_Value");
                if (cgmTrans != null) cgmValueText = cgmTrans.GetComponent<TMPro.TextMeshProUGUI>();

                Transform goldTrans = iconLine.Find("Gold/Gold_Value");
                if (goldTrans != null) goldValueText = goldTrans.GetComponent<TMPro.TextMeshProUGUI>();

                Transform curLevTrans = iconLine.Find("Current_Level/Current_Levle_Value");
                if (curLevTrans != null) currentLevelText = curLevTrans.GetComponent<TMPro.TextMeshProUGUI>();

                Transform nextLevTrans = iconLine.Find("Next_Level/Next_Levle_Value");
                if (nextLevTrans != null) nextLevelText = nextLevTrans.GetComponent<TMPro.TextMeshProUGUI>();

                Transform cardsTrans = iconLine.Find("Cards/Text (TMP)");
                if (cardsTrans != null) cardsCountText = cardsTrans.GetComponent<TMPro.TextMeshProUGUI>();

                Transform settingTrans = iconLine.Find("Setting");
                if (settingTrans != null) settingButton = settingTrans.GetComponent<UnityEngine.UI.Button>();
            }

            playerStats = FindObjectOfType<PlayerStats>();
            battleController = FindObjectOfType<BattleSessionController>();

            if (transform.parent != null)
            {
                Transform spTrans = transform.parent.Find("SettingPanel");
                if (spTrans != null) settingPanel = spTrans.gameObject;
            }
        }
#endif
    }
}
