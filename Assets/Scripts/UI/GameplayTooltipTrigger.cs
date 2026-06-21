using UnityEngine;
using UnityEngine.EventSystems;
using CGM.Core;
using CGM.Data;

namespace CGM.UI
{
    /// <summary>
    /// 通用游戏悬停提示触发器。
    /// 集成意图、能量、牌堆、生命值、血糖、Buff状态栏等所有战役相关主体的悬停描述展示。
    /// </summary>
    public class GameplayTooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private string tooltipId;

        private BattleSessionController _battleController;
        private PlayerStats _playerStats;
        private EnemyStats _enemyStats;
        private bool _isHovered;
        private string _lastContent;

        /// <summary>
        /// 配置触发器的提示 ID
        /// </summary>
        public void Setup(string id)
        {
            tooltipId = id;
            if (_isHovered)
            {
                _lastContent = null;
                ShowTooltip();
            }
        }

        private void Start()
        {
            _battleController = FindObjectOfType<BattleSessionController>();
            _playerStats = FindObjectOfType<PlayerStats>();
            _enemyStats = FindObjectOfType<EnemyStats>();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _isHovered = true;
            ShowTooltip();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _isHovered = false;
            HideTooltip();
        }

        private void OnDisable()
        {
            _isHovered = false;
            HideTooltip();
        }

        private void Update()
        {
            if (_isHovered)
            {
                ShowTooltip();
            }
        }

        private void ShowTooltip()
        {
            if (TooltipManager.Instance == null || string.IsNullOrEmpty(tooltipId)) return;

            // 如果当前有其他子节点正在显示 tooltip，则父节点不应该覆写它
            if (TooltipManager.Instance.CurrentOwner != null && 
                TooltipManager.Instance.CurrentOwner != transform && 
                TooltipManager.Instance.CurrentOwner.IsChildOf(transform))
            {
                return;
            }

            // 特殊处理肥胖领主 (fat_lord) 的双描述框
            if (tooltipId == "enemy_desc")
            {
                if (_enemyStats == null) _enemyStats = FindObjectOfType<EnemyStats>();
                if (_enemyStats != null && _enemyStats.EnemyId == "fat_lord" && _enemyStats.EnemyInfo != null)
                {
                    string name = _enemyStats.EnemyInfo.name;
                    string descStr = _enemyStats.EnemyInfo.description;
                    string bossDesc = $"<color=#FF6B6B><b>{name}</b></color>\n{descStr}";
                    string effectDesc = $"<color=#FFAD1F><b>代谢复苏</b></color>\n击败该领主后将恢复一定生命值。";

                    string combinedKey = bossDesc + "|" + effectDesc;
                    if (combinedKey != _lastContent)
                    {
                        _lastContent = combinedKey;
                        var list = new System.Collections.Generic.List<string> { bossDesc, effectDesc };
                        TooltipManager.Instance.ShowMultipleTooltips(transform as RectTransform, list);
                    }
                    return;
                }
            }

            string tooltipText = GetFormattedTooltipText();
            if (string.IsNullOrEmpty(tooltipText)) return;

            if (tooltipText != _lastContent)
            {
                _lastContent = tooltipText;
                TooltipManager.Instance.ShowTooltip(tooltipText, transform as RectTransform);
            }
        }

        private void HideTooltip()
        {
            _lastContent = null;
            if (TooltipManager.Instance != null)
            {
                TooltipManager.Instance.HideTooltip(transform as RectTransform);
                TooltipManager.Instance.HideLoreTooltip(transform as RectTransform);
            }
        }

        private string GetFormattedTooltipText()
        {
            // 1. 状态栏 Buff/Debuff 图标悬停
            if (tooltipId.StartsWith("buff:"))
            {
                string buffIdStr = tooltipId.Substring("buff:".Length);
                if (System.Enum.TryParse<BuffId>(buffIdStr, true, out var buffId))
                {
                    var buffInfo = BuffDatabase.Get(buffId);
                    if (buffInfo != null)
                    {
                        string titleColor = buffInfo.colorHex;
                        if (string.IsNullOrEmpty(titleColor)) titleColor = "#FFFFFF";
                        return $"<color={titleColor}><b>{buffInfo.name}</b></color>\n{buffInfo.description}";
                    }
                }
                return "";
            }

            // 2. 敌人头像悬停 (敌人信息)
            if (tooltipId == "enemy_desc")
            {
                if (_enemyStats == null) _enemyStats = FindObjectOfType<EnemyStats>();
                if (_enemyStats != null && _enemyStats.EnemyInfo != null)
                {
                    string name = _enemyStats.EnemyInfo.name;
                    string descStr = _enemyStats.EnemyInfo.description;
                    string titleColor = "#FF6B6B"; // 敌人使用红色高亮标题
                    return $"<color={titleColor}><b>{name}</b></color>\n{descStr}";
                }
                return "";
            }

            // 3. 当前场景/关卡悬停 (场景信息)
            if (tooltipId == "scene_desc")
            {
                if (LevelManager.Instance != null && LevelManager.Instance.CurrentNode != null)
                {
                    var node = LevelManager.Instance.CurrentNode;
                    string levelName = node.levelName;
                    string levelDesc = "";

                    if (node.type == LevelType.Shop)
                    {
                        levelDesc = "补给与调整牌组的场所。\n可以使用金币购买强力卡牌或移除卡牌。";
                    }
                    else
                    {
                        if (_enemyStats == null) _enemyStats = FindObjectOfType<EnemyStats>();
                        if (_enemyStats != null && _enemyStats.EnemyInfo != null && _enemyStats.EnemyInfo.id == node.enemyId)
                        {
                            levelDesc = _enemyStats.EnemyInfo.levelDescription;
                        }
                        else if (EnemyDatabase.Instance != null)
                        {
                            var enemyInfo = EnemyDatabase.Instance.GetEnemyById(node.enemyId);
                            if (enemyInfo != null)
                            {
                                levelDesc = enemyInfo.levelDescription;
                            }
                        }
                    }

                    if (string.IsNullOrEmpty(levelDesc))
                    {
                        levelDesc = "起伏不定的代谢战役关卡。";
                    }

                    string titleColor = "#FFAD1F"; // 关卡场景使用亮橘色标题
                    return $"<color={titleColor}><b>{levelName}</b></color>\n{levelDesc}";
                }
                return "";
            }

            // 4. 下一个场景/关卡悬停 (下个场景信息)
            if (tooltipId == "next_scene_desc")
            {
                if (LevelManager.Instance != null && LevelManager.Instance.NextNode != null)
                {
                    var node = LevelManager.Instance.NextNode;
                    string levelName = node.levelName;
                    string levelDesc = "";

                    if (node.type == LevelType.Shop)
                    {
                        levelDesc = "补给与调整牌组的场所。可以使用金币购买强力卡牌或移除卡牌。";
                    }
                    else if (EnemyDatabase.Instance != null)
                    {
                        var enemyInfo = EnemyDatabase.Instance.GetEnemyById(node.enemyId);
                        if (enemyInfo != null)
                        {
                            levelDesc = enemyInfo.levelDescription;
                        }
                    }

                    if (string.IsNullOrEmpty(levelDesc))
                    {
                        levelDesc = "起伏不定的代谢战役关卡。";
                    }

                    string titleColor = "#888888"; // 下一关详情使用淡灰/暗色标题
                    return $"<color={titleColor}><b>下一关：{levelName}</b></color>\n{levelDesc}";
                }
                return "";
            }

            // 5. 动态血糖展示文本悬停
            if (tooltipId == "glucose")
            {
                if (_playerStats == null) _playerStats = FindObjectOfType<PlayerStats>();
                float glucose = _playerStats != null ? _playerStats.CurrentGlucose : 5.7f;
                string rawId;
                if (glucose < BattleConstants.HealthyGlucoseMin)
                    rawId = "glucose_low";
                else if (glucose <= BattleConstants.HealthyGlucoseMax)
                    rawId = "glucose_healthy";
                else
                    rawId = "glucose_high";

                TooltipInfo info = BuffDatabase.GetRawTooltip(rawId);
                if (info != null)
                {
                    string titleColor = info.colorHex;
                    if (string.IsNullOrEmpty(titleColor)) titleColor = "#FFFFFF";
                    return $"<color={titleColor}><b>当前状态：{info.name}</b></color>\n{info.description}";
                }
                return "";
            }

            // 6. 配置表通用悬停数据读取及占位符动态填充
            TooltipInfo tooltipInfo = BuffDatabase.GetRawTooltip(tooltipId);
            if (tooltipInfo == null) return "";

            string desc = tooltipInfo.description;
            if (string.IsNullOrEmpty(desc)) return "";

            if (tooltipId == "attack_intent")
            {
                if (_enemyStats == null) _enemyStats = FindObjectOfType<EnemyStats>();
                if (_playerStats == null) _playerStats = FindObjectOfType<PlayerStats>();
                if (_enemyStats != null && _playerStats != null)
                {
                    EnemyIntentInfo intent = _enemyStats.GetCurrentIntent();
                    if (intent != null && intent.actionType == "attack")
                    {
                        int baseDmg = intent.GetValue();
                        int scaledDmg = Mathf.CeilToInt(baseDmg * _enemyStats.TurnScaling);
                        int realDmg = BattleCalculator.CalculateDamage(scaledDmg, _enemyStats, _playerStats);
                        desc = desc.Replace("{D}", $"<color={BattleConstants.ColorRed}>{realDmg}</color>");
                    }
                    else
                    {
                        desc = desc.Replace("{D}", "0");
                    }
                }
            }
            else if (tooltipId == "block_intent")
            {
                if (_enemyStats == null) _enemyStats = FindObjectOfType<EnemyStats>();
                if (_enemyStats != null)
                {
                    EnemyIntentInfo intent = _enemyStats.GetCurrentIntent();
                    if (intent != null && intent.actionType == "block")
                    {
                        int baseBlk = intent.GetValue();
                        int scaledBlk = Mathf.CeilToInt(baseBlk * _enemyStats.TurnScaling);
                        int realBlk = BattleCalculator.CalculateBlock(scaledBlk, _enemyStats);
                        desc = desc.Replace("{B}", $"<color={BattleConstants.ColorGreen}>{realBlk}</color>");
                    }
                    else
                    {
                        desc = desc.Replace("{B}", "0");
                    }
                }
            }
            else if (tooltipId == "energy")
            {
                if (_battleController == null) _battleController = FindObjectOfType<BattleSessionController>();
                if (_battleController != null)
                {
                    desc = desc.Replace("{E}", _battleController.CurrentEnergy.ToString());
                    desc = desc.Replace("{M}", _battleController.MaxEnergy.ToString());
                }
            }
            else if (tooltipId == "draw_pile")
            {
                if (_battleController == null) _battleController = FindObjectOfType<BattleSessionController>();
                if (_battleController != null)
                {
                    desc = desc.Replace("{D}", _battleController.DrawPile.Count.ToString());
                    desc = desc.Replace("{H}", _battleController.Hand.Count.ToString());
                    desc = desc.Replace("{C}", "10"); // maximumHandSize 固为 10
                }
            }
            else if (tooltipId == "discard_pile")
            {
                if (_battleController == null) _battleController = FindObjectOfType<BattleSessionController>();
                if (_battleController != null)
                {
                    desc = desc.Replace("{DP}", _battleController.DiscardPile.Count.ToString());
                }
            }
            else if (tooltipId == "hp" || tooltipId == "enemy_hp")
            {
                EntityStats entity = GetComponentInParent<EntityStats>();
                if (entity != null)
                {
                    desc = desc.Replace("{H}", entity.CurrentHp.ToString());
                    desc = desc.Replace("{M}", entity.MaxHp.ToString());
                }
                else
                {
                    if (_playerStats == null) _playerStats = FindObjectOfType<PlayerStats>();
                    if (_playerStats != null)
                    {
                        desc = desc.Replace("{H}", _playerStats.CurrentHp.ToString());
                        desc = desc.Replace("{M}", _playerStats.MaxHp.ToString());
                    }
                }
            }

            string headColor = tooltipInfo.colorHex;
            if (string.IsNullOrEmpty(headColor)) headColor = "#FFFFFF";

            return $"<color={headColor}><b>{tooltipInfo.name}</b></color>\n{desc}";
        }
    }
}
