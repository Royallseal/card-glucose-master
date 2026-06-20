// =============================================================================
// EnemyUI.cs — 敌人 UI 表现层控制器
// 命名空间：CGM.UI
// 职责：动态渲染敌人的血条、格挡值、当前身上 Buff 图标以及动态受增减益影响的意图数值。
// =============================================================================

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CGM.Core;
using CGM.Data;

namespace CGM.UI
{
    /// <summary>
    /// 敌人实体的 UI 渲染类，配合 EnemyStats 实时呈现血量、格挡、Buff 与实时意图。
    /// </summary>
    public class EnemyUI : MonoBehaviour
    {
        [Header("基础 UI 引用")]
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private Image enemyImage;               // 敌人全身像
        [SerializeField] private Slider hpSlider;
        [SerializeField] private TextMeshProUGUI hpText;
        
        [Header("格挡 UI 引用")]
        [SerializeField] private GameObject blockContainer;      // 格挡 UI 容器
        [SerializeField] private TextMeshProUGUI blockText;      // 格挡数值文本

        [Header("行动意图 UI 引用")]
        [SerializeField] private GameObject intentContainer;     // 意图 UI 容器
        [SerializeField] private Image intentIcon;               // 意图图标
        [SerializeField] private TextMeshProUGUI intentValueText;// 意图数值/层数文本

        [Header("Buff/Debuff UI 容器")]
        [SerializeField] private Transform buffContainer;        // Buff 图标挂载父节点
        [SerializeField] private GameObject buffIconPrefab;      // Buff 图标 Prefab (Resources/Prefabs/BuffIcon)
        [SerializeField] private GameObject targetIndicator;     // 锁定指示器（拖拽卡牌时显示）

        private EnemyStats _enemyStats;
        private PlayerStats _cachedPlayer;

        private void Start()
        {
            // 自动查找自身或父节点上的 EnemyStats
            if (_enemyStats == null)
            {
                _enemyStats = GetComponent<EnemyStats>();
                if (_enemyStats == null)
                    _enemyStats = GetComponentInParent<EnemyStats>();
            }
            if (_enemyStats != null)
            {
                _enemyStats.OnStatsChanged += RefreshUI;
                RefreshUI();
            }

            // 血条只显示，不响应拖拽
            if (hpSlider != null) hpSlider.interactable = false;

            // 缓存玩家 Stats，用于计算意图受击伤害
            if (_cachedPlayer == null)
            {
                _cachedPlayer = FindObjectOfType<PlayerStats>();
            }

            // 绑定生命值、格挡和头像悬停提示
            if (hpSlider != null)
            {
                var trigger = hpSlider.gameObject.GetComponent<GameplayTooltipTrigger>();
                if (trigger == null) trigger = hpSlider.gameObject.AddComponent<GameplayTooltipTrigger>();
                trigger.Setup("hp");
            }
            if (blockContainer != null)
            {
                var trigger = blockContainer.GetComponent<GameplayTooltipTrigger>();
                if (trigger == null) trigger = blockContainer.AddComponent<GameplayTooltipTrigger>();
                trigger.Setup("current_block");
            }
            if (enemyImage != null)
            {
                var trigger = enemyImage.gameObject.GetComponent<GameplayTooltipTrigger>();
                if (trigger == null) trigger = enemyImage.gameObject.AddComponent<GameplayTooltipTrigger>();
                trigger.Setup("enemy_desc");
            }
        }

        private void OnDestroy()
        {
            // 防止内存泄漏，注销事件
            if (_enemyStats != null)
            {
                _enemyStats.OnStatsChanged -= RefreshUI;
            }
        }

        /// <summary>
        /// 绑定敌人数据源，并订阅状态变更事件。
        /// </summary>
        public void SetEnemy(EnemyStats stats)
        {
            if (_enemyStats != null)
            {
                _enemyStats.OnStatsChanged -= RefreshUI;
            }

            _enemyStats = stats;

            if (_enemyStats != null)
            {
                _enemyStats.OnStatsChanged += RefreshUI;
                RefreshUI();
            }
        }

        /// <summary>
        /// 刷新敌人的所有 UI 表现（血条、格挡、当前意图、Buff 图标）。
        /// </summary>
        public void RefreshUI()
        {
            if (_enemyStats == null || _enemyStats.EnemyInfo == null)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);

            // 1. 名字、精灵图与基础血量
            nameText.text = _enemyStats.EnemyInfo.name;
            if (enemyImage != null)
            {
                string spritePath = $"Sprites/Characters/Enemies/{_enemyStats.EnemyInfo.name}";
                Sprite sp = Resources.Load<Sprite>(spritePath);
                if (sp != null) enemyImage.sprite = sp;
            }
            hpSlider.maxValue = _enemyStats.MaxHp;
            hpSlider.value = _enemyStats.CurrentHp;
            hpText.text = $"{_enemyStats.CurrentHp}/{_enemyStats.MaxHp}";

            // 2. 格挡值展示
            int currentBlock = _enemyStats.Block;
            if (currentBlock > 0)
            {
                blockText.text = currentBlock.ToString();
                blockContainer.SetActive(true);
            }
            else
            {
                blockContainer.SetActive(false);
            }

            // 3. 当前意图 (Intent) 实时计算渲染
            RefreshIntent();

            // 4. Buff 图标列表实例化与刷新
            RefreshBuffIcons();
        }

        /// <summary>
        /// 刷新意图渲染，自动根据当前状态动态重算攻击与格挡，并高亮显示。
        /// </summary>
        private void RefreshIntent()
        {
            EnemyIntentInfo intent = _enemyStats.GetCurrentIntent();
            if (intent == null)
            {
                intentContainer.SetActive(false);
                return;
            }

            intentContainer.SetActive(true);

            // 确保玩家引用有效
            if (_cachedPlayer == null)
            {
                _cachedPlayer = FindObjectOfType<PlayerStats>();
            }

            var intentTrigger = intentContainer.GetComponent<GameplayTooltipTrigger>();
            if (intentTrigger == null) intentTrigger = intentContainer.AddComponent<GameplayTooltipTrigger>();
            if (intentTrigger != null)
            {
                intentTrigger.Setup(intent.actionType switch
                {
                    "attack" => "attack_intent",
                    "block" => "block_intent",
                    "buff" => "status_intent",
                    "debuff" => "status_intent",
                    _ => ""
                });
            }

            switch (intent.actionType)
            {
                case "attack":
                    intentIcon.sprite = Resources.Load<Sprite>("UI/Icons/intent_attack");
                    // 实时计算攻击意图数值：综合敌人自身 Buff（活力/乏力）、玩家 Debuff（脆弱）、血糖修正、回合强度缩放
                    int baseDmg = intent.GetValue();
                    int scaledDmg = Mathf.CeilToInt(baseDmg * _enemyStats.TurnScaling);
                    int realDmg = BattleCalculator.CalculateDamage(scaledDmg, _enemyStats, _cachedPlayer);
                    intentValueText.text = realDmg.ToString();
                    intentValueText.gameObject.SetActive(true);
                    break;

                case "block":
                    intentIcon.sprite = Resources.Load<Sprite>("UI/Icons/intent_block");
                    // 实时计算格挡意图数值：综合敌人自身 Buff（耐力/僵硬）、血糖修正、回合强度缩放
                    int baseBlk = intent.GetValue();
                    int scaledBlk = Mathf.CeilToInt(baseBlk * _enemyStats.TurnScaling);
                    int realBlk = BattleCalculator.CalculateBlock(scaledBlk, _enemyStats);
                    intentValueText.text = realBlk.ToString();
                    intentValueText.gameObject.SetActive(true);
                    break;

                case "buff":
                case "debuff":
                    intentIcon.sprite = Resources.Load<Sprite>("UI/Icons/intent_status");
                    intentValueText.gameObject.SetActive(false);
                    break;

                default:
                    intentContainer.SetActive(false);
                    break;
            }
        }

        /// <summary>
        /// 重新绘制身上的所有活跃 Buff 图标。
        /// </summary>
        private void RefreshBuffIcons()
        {
            if (buffContainer == null || buffIconPrefab == null) return;

            // 清理旧图标
            foreach (Transform child in buffContainer)
            {
                Destroy(child.gameObject);
            }

            var activeBuffs = _enemyStats.GetAllActiveBuffs();
            foreach (var kvp in activeBuffs)
            {
                BuffId id = kvp.Key;
                int stacks = kvp.Value;

                if (stacks == 0) continue;

                // 实例化图标预制体
                GameObject iconGo = Instantiate(buffIconPrefab, buffContainer);
                iconGo.name = $"Buff_{id}";

                // 查找渲染组件
                Image img = iconGo.GetComponent<Image>();
                TextMeshProUGUI txt = iconGo.GetComponentInChildren<TextMeshProUGUI>();

                // 载入对应的图标 Sprite
                string spritePath = BuffDatabase.GetSpritePath(id);
                Sprite sp = Resources.Load<Sprite>(spritePath);
                if (img != null && sp != null)
                {
                    img.sprite = sp;
                }

                // 应用状态对应颜色
                if (img != null)
                {
                    var info = BuffDatabase.Get(id);
                    if (info != null && ColorUtility.TryParseHtmlString(info.colorHex, out Color c))
                        img.color = c;
                    else
                        img.color = Color.white;
                }

                // 呈现状态层数
                if (txt != null)
                {
                    txt.text = stacks.ToString();
                }

                // 悬停描述
                var hover = iconGo.GetComponent<GameplayTooltipTrigger>();
                if (hover == null) hover = iconGo.AddComponent<GameplayTooltipTrigger>();
                hover.Setup($"buff:{id}");
            }
        }

        /// <summary>
        /// 显示或隐藏锁定指示器（拖拽卡牌悬停时使用）。
        /// </summary>
        public void ShowTargetIndicator(bool show)
        {
            if (targetIndicator != null)
                targetIndicator.SetActive(show);
        }
    }
}
