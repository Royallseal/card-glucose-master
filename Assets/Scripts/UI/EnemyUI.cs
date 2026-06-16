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
        [SerializeField] private TextMeshProUGUI nameText;       // 敌人名字
        [SerializeField] private Slider hpSlider;                // 血量进度条
        [SerializeField] private TextMeshProUGUI hpText;         // 血量数值文案 (如 "45/45")
        
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

        private EnemyStats _enemyStats;
        private PlayerStats _cachedPlayer;

        private void Start()
        {
            // 缓存玩家 Stats，用于计算意图受击伤害
            if (_cachedPlayer == null)
            {
                _cachedPlayer = FindObjectOfType<PlayerStats>();
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

            // 1. 名字与基础血量
            nameText.text = _enemyStats.EnemyInfo.name;
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

            switch (intent.actionType)
            {
                case "attack":
                    // 加载攻击意图图标
                    intentIcon.sprite = Resources.Load<Sprite>("Sprites/UI/Icons/intent_attack");
                    
                    // 实时进行受击公式计算 (包含玩家的脆弱、敌方的乏力和活力等)
                    int baseDamage = intent.GetValue();
                    int finalDamage = BattleCalculator.CalculateDamage(baseDamage, _enemyStats, _cachedPlayer);
                    
                    // 对比初始值进行红绿变色渲染
                    if (finalDamage > baseDamage)
                    {
                        intentValueText.text = $"<color={BattleConstants.ColorGreen}>{finalDamage}</color>";
                    }
                    else if (finalDamage < baseDamage)
                    {
                        intentValueText.text = $"<color={BattleConstants.ColorRed}>{finalDamage}</color>";
                    }
                    else
                    {
                        intentValueText.text = finalDamage.ToString();
                    }
                    break;

                case "block":
                    // 加载格挡意图图标
                    intentIcon.sprite = Resources.Load<Sprite>("Sprites/UI/Icons/intent_block");
                    
                    // 实时计算最终格挡 (考虑僵硬、耐力等)
                    int baseBlock = intent.GetValue();
                    int finalBlock = BattleCalculator.CalculateBlock(baseBlock, _enemyStats);
                    
                    // 对比初始值变色
                    if (finalBlock > baseBlock)
                    {
                        intentValueText.text = $"<color={BattleConstants.ColorGreen}>{finalBlock}</color>";
                    }
                    else if (finalBlock < baseBlock)
                    {
                        intentValueText.text = $"<color={BattleConstants.ColorRed}>{finalBlock}</color>";
                    }
                    else
                    {
                        intentValueText.text = finalBlock.ToString();
                    }
                    break;

                case "buff":
                case "debuff":
                    // 从 parameter1 中解析具体的 BuffId，将其对应的状态图标作为意图展现
                    if (System.Enum.TryParse<BuffId>(intent.parameter1, true, out var targetBuffId))
                    {
                        intentIcon.sprite = Resources.Load<Sprite>(GetBuffSpritePath(targetBuffId));
                    }
                    else
                    {
                        intentIcon.sprite = Resources.Load<Sprite>("Sprites/UI/Icons/intent_status");
                    }
                    // 意图数值文本直接呈现 Buff 层数 (白字)
                    intentValueText.text = intent.parameter2.ToString();
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
                string spritePath = GetBuffSpritePath(id);
                Sprite sp = Resources.Load<Sprite>(spritePath);
                if (img != null && sp != null)
                {
                    img.sprite = sp;
                }

                // 呈现状态层数
                if (txt != null)
                {
                    txt.text = stacks.ToString();
                }
            }
        }

        /// <summary>
        /// 获取 BuffId 对应图片资源在 Resources 目录下的相对路径。
        /// </summary>
        private string GetBuffSpritePath(BuffId id)
        {
            switch (id)
            {
                case BuffId.Vitality:    return "Sprites/UI/Icons/buff_vitality";
                case BuffId.Endurance:   return "Sprites/UI/Icons/buff_endurance";
                case BuffId.Fragility:   return "Sprites/UI/Icons/debuff_fragility";
                case BuffId.Lethargy:    return "Sprites/UI/Icons/debuff_lethargy";
                case BuffId.Stiffness:   return "Sprites/UI/Icons/debuff_stiffness";
                case BuffId.SlowRelease: return "Sprites/UI/Icons/buff_slow_release";
                case BuffId.Sensitivity: return "Sprites/UI/Icons/buff_sensitivity";
                default:                 return "Sprites/UI/Icons/intent_status";
            }
        }
    }
}
