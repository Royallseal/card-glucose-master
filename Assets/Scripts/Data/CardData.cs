// =============================================================================
// CardData.cs — 卡牌数据实体定义
// 命名空间：CGM.Data
// 职责：定义卡牌系统所需的全部枚举类型与可序列化数据结构。
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace CGM.Data
{
    /// <summary>
    /// 卡牌类型枚举，对应四种卡牌流派。
    /// </summary>
    public enum CardType
    {
        Starter,    // 初始卡
        Diet,       // 膳食卡
        Exercise,   // 运动卡
        Medicine    // 药物卡
    }

    /// <summary>
    /// 卡牌稀有度枚举。
    /// </summary>
    public enum CardRarity
    {
        Common,     // 普通
        Uncommon,   // 良好
        Rare        // 优秀
    }

    /// <summary>
    /// 效果类型枚举，对应 card_effects.csv 中的 effectType 列。
    /// </summary>
    public enum EffectType
    {
        Hit,            // 多段攻击（value1 = 攻击次数）
        Draw,           // 抽牌（value1 = 抽牌数量）
        SelfDamage,     // 自伤（value1 = 失去的生命值）
        ApplyBuff,      // 施加增益状态（value1 = 状态 ID，value2 = 层数）
        ApplyDebuff,    // 施加减益状态（value1 = 状态 ID，value2 = 层数）
        GlucoseCap      // 血糖封顶（value1 = 阈值）
    }

    /// <summary>
    /// Buff/Debuff 状态标识枚举，与 design_buff_system.md 的定义保持一致。
    /// </summary>
    public enum BuffId
    {
        Vitality,       // 活力：每层使攻击伤害 +1
        Endurance,      // 耐力：每层使格挡值 +1
        Fragility,      // 脆弱：承受伤害 +50%，每回合 -1 层
        Lethargy,       // 乏力：造成伤害 -25%，每回合 -1 层
        Stiffness,      // 僵硬：获得格挡 -25%，每回合 -1 层
        SlowRelease,    // 缓释：抵消下一张膳食卡的升糖效果
        Sensitivity     // 敏化：使下一张药物卡降糖效果翻倍
    }

    /// <summary>
    /// Buff/Debuff 状态静态信息结构，用于 Tooltip 说明及 UI 渲染。
    /// </summary>
    [Serializable]
    public class BuffInfo
    {
        public BuffId id;
        public string name;
        public string description;
        public string colorHex;
        public bool isDebuff;

        public BuffInfo(BuffId id, string name, string description, string colorHex, bool isDebuff)
        {
            this.id = id;
            this.name = name;
            this.description = description;
            this.colorHex = colorHex;
            this.isDebuff = isDebuff;
        }
    }

    /// <summary>
    /// 描述框数据实体定义（对应 tooltips.json 中的单个对象）。
    /// </summary>
    [System.Serializable]
    public class TooltipInfo
    {
        public string id;
        public string name;
        public string description;
        public string colorHex;
        public bool isDebuff;
        public string spritePath;
    }

    /// <summary>
    /// 描述框数据包装类。
    /// </summary>
    [System.Serializable]
    public class TooltipDataWrapper
    {
        public List<TooltipInfo> tooltips = new List<TooltipInfo>();
    }

    /// <summary>
    /// 状态静态数据库，定义所有 Buff/Debuff 的名称、说明及高亮颜色。
    /// </summary>
    public static class BuffDatabase
    {
        private static Dictionary<BuffId, BuffInfo> _registry;
        private static Dictionary<string, TooltipInfo> _rawTooltips;
        private static readonly Dictionary<string, BuffId> _nameToIdMap = new Dictionary<string, BuffId>();

        /// <summary>
        /// 懒加载配置的 JSON 状态描述数据。
        /// </summary>
        public static void LoadIfNeeded()
        {
            if (_registry != null) return;

            _registry = new Dictionary<BuffId, BuffInfo>();
            _rawTooltips = new Dictionary<string, TooltipInfo>();
            _nameToIdMap.Clear();

            TextAsset jsonAsset = UnityEngine.Resources.Load<TextAsset>("Configs/tooltips");
            if (jsonAsset == null)
            {
                UnityEngine.Debug.LogError("[BuffDatabase] 未找到描述框数据文件 Resources/Configs/tooltips.json，使用硬编码兜底。");
                LoadFallbackRegistry();
                return;
            }

            TooltipDataWrapper wrapper = UnityEngine.JsonUtility.FromJson<TooltipDataWrapper>(jsonAsset.text);
            if (wrapper == null || wrapper.tooltips == null)
            {
                UnityEngine.Debug.LogError("[BuffDatabase] 描述框数据反序列化失败，使用硬编码兜底。");
                LoadFallbackRegistry();
                return;
            }

            foreach (var t in wrapper.tooltips)
            {
                _rawTooltips[t.id] = t;

                if (System.Enum.TryParse<BuffId>(t.id, true, out var buffId))
                {
                    _registry[buffId] = new BuffInfo(buffId, t.name, t.description, t.colorHex, t.isDebuff);
                    _nameToIdMap[t.name] = buffId;
                }
            }
        }

        private static void LoadFallbackRegistry()
        {
            _registry = new Dictionary<BuffId, BuffInfo>
            {
                { BuffId.Vitality, new BuffInfo(BuffId.Vitality, "活力", "每层使打出的攻击卡伤害 +1（可为负数）。", "#4EC9B0", false) },
                { BuffId.Endurance, new BuffInfo(BuffId.Endurance, "耐力", "每层使打出的防守卡格挡值 +1（可为负数）。", "#4EC9B0", false) },
                { BuffId.Fragility, new BuffInfo(BuffId.Fragility, "脆弱", "受到伤害时，受到的伤害提升 50%。每回合结束层数 -1。", "#FF6B6B", true) },
                { BuffId.Lethargy, new BuffInfo(BuffId.Lethargy, "乏力", "造成伤害时，输出的伤害降低 25%。每回合结束层数 -1。", "#FF6B6B", true) },
                { BuffId.Stiffness, new BuffInfo(BuffId.Stiffness, "僵硬", "获得格挡时，获得的格挡值降低 25%。每回合结束层数 -1。", "#FF6B6B", true) },
                { BuffId.SlowRelease, new BuffInfo(BuffId.SlowRelease, "缓释", "抵消下一张膳食卡的血糖上升效果。触发后消耗 1 层。", "#FFAD1F", false) },
                { BuffId.Sensitivity, new BuffInfo(BuffId.Sensitivity, "敏化", "使下一张药物卡的降糖效果翻倍。触发后消耗 1 层。", "#FFAD1F", false) }
            };

            foreach (var kvp in _registry)
            {
                _nameToIdMap[kvp.Value.name] = kvp.Key;
            }
        }

        public static BuffInfo Get(BuffId id)
        {
            LoadIfNeeded();
            if (_registry.TryGetValue(id, out var info))
            {
                return info;
            }
            return null;
        }

        public static TooltipInfo GetRawTooltip(string rawId)
        {
            LoadIfNeeded();
            if (_rawTooltips != null && _rawTooltips.TryGetValue(rawId, out var info))
            {
                return info;
            }
            return null;
        }

        public static bool TryGetIdByName(string name, out BuffId id)
        {
            LoadIfNeeded();
            return _nameToIdMap.TryGetValue(name, out id);
        }

        public static IEnumerable<BuffInfo> GetAllBuffs()
        {
            LoadIfNeeded();
            return _registry.Values;
        }

        /// <summary>
        /// 根据 BuffId 返回对应的精灵资源路径。
        /// </summary>
        public static string GetSpritePath(BuffId id)
        {
            LoadIfNeeded();
            string key = id.ToString().ToLower();
            if (_rawTooltips != null && _rawTooltips.TryGetValue(key, out var t) && !string.IsNullOrEmpty(t.spritePath))
            {
                return t.spritePath;
            }

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

    // =========================================================================

    // 可序列化数据结构（供 JsonUtility 使用）
    // =========================================================================

    /// <summary>
    /// 卡牌单条效果数据，对应 card_effects.csv 中的一行记录。
    /// </summary>
    [Serializable]
    public class CardEffect
    {
        /// <summary>效果类型标识，对应 EffectType 枚举名称。</summary>
        public string effectType;

        /// <summary>效果参数 1（语义因效果类型而异）。</summary>
        public string value1;

        /// <summary>效果参数 2（语义因效果类型而异，可为空）。</summary>
        public string value2;

        /// <summary>
        /// 将 effectType 字符串解析为 EffectType 枚举值。
        /// </summary>
        public EffectType GetEffectType()
        {
            // 将 CSV 中的 snake_case 映射为枚举 PascalCase
            switch (effectType)
            {
                case "hit":          return EffectType.Hit;
                case "draw":         return EffectType.Draw;
                case "self_damage":  return EffectType.SelfDamage;
                case "apply_buff":   return EffectType.ApplyBuff;
                case "apply_debuff": return EffectType.ApplyDebuff;
                case "glucose_cap":  return EffectType.GlucoseCap;
                default:
                    throw new ArgumentException($"未知的效果类型：{effectType}");
            }
        }

        /// <summary>
        /// 将 value1 中的状态 ID 字符串解析为 BuffId 枚举值。
        /// 仅在 effectType 为 apply_buff 或 apply_debuff 时使用。
        /// </summary>
        public BuffId GetBuffId()
        {
            switch (value1)
            {
                case "vitality":     return BuffId.Vitality;
                case "endurance":    return BuffId.Endurance;
                case "fragility":    return BuffId.Fragility;
                case "lethargy":     return BuffId.Lethargy;
                case "stiffness":    return BuffId.Stiffness;
                case "slow_release": return BuffId.SlowRelease;
                case "sensitivity":  return BuffId.Sensitivity;
                default:
                    throw new ArgumentException($"未知的状态 ID：{value1}");
            }
        }

        /// <summary>
        /// 将 value1 解析为整数（用于 hit、draw、self_damage 等效果）。
        /// </summary>
        public int GetIntValue1()
        {
            return int.Parse(value1);
        }

        /// <summary>
        /// 将 value1 解析为浮点数（用于 glucose_cap 等效果）。
        /// </summary>
        public float GetFloatValue1()
        {
            return float.Parse(value1);
        }

        /// <summary>
        /// 将 value2 解析为整数（用于 buff/debuff 的层数）。
        /// </summary>
        public int GetIntValue2()
        {
            return int.Parse(value2);
        }
    }

    /// <summary>
    /// 卡牌完整数据结构，包含主表基础属性与效果表的特殊效果列表。
    /// </summary>
    [Serializable]
    public class CardInfo
    {
        public string id;
        public string name;
        public string type;
        public string rarity;
        public int energyCost;
        public float glucoseChange;
        public int finalDamage;
        public int finalBlock;
        public string description;
        public string iconColor;
        public List<CardEffect> effects = new List<CardEffect>();

        /// <summary>
        /// 获取动态卡牌描述，并在数值发生改变时使用富文本高亮显示。
        /// </summary>
        /// <param name="modifierDamage">运行时攻击增益（默认为 0）</param>
        /// <param name="modifierBlock">运行时格挡增益（默认为 0）</param>
        /// <param name="glucoseMultiplier">血糖变化倍率（默认为 1.0f，高血糖 Hyper 状态下应传入 2.0f 触发红字预警）</param>
        /// <returns>最终带富文本的描述字符串</returns>
        public string GetDynamicDescription(int modifierDamage = 0, int modifierBlock = 0, float glucoseMultiplier = 1.0f)
        {
            if (string.IsNullOrEmpty(description)) return "";

            string result = description;

            // 1. 处理伤害占位符 {D}
            if (finalDamage > 0 || result.Contains("{D}"))
            {
                int currentDamage = System.Math.Max(0, finalDamage + modifierDamage);
                string damageStr = currentDamage.ToString();
                if (modifierDamage > 0)
                {
                    damageStr = $"<color={CGM.Core.BattleConstants.ColorGreen}>{currentDamage}</color>"; // 增益显示绿色
                }
                else if (modifierDamage < 0)
                {
                    damageStr = $"<color={CGM.Core.BattleConstants.ColorRed}>{currentDamage}</color>"; // 减益显示红色
                }
                result = result.Replace("{D}", damageStr);
            }

            // 2. 处理格挡占位符 {B}
            if (finalBlock > 0 || result.Contains("{B}"))
            {
                int currentBlock = System.Math.Max(0, finalBlock + modifierBlock);
                string blockStr = currentBlock.ToString();
                if (modifierBlock > 0)
                {
                    blockStr = $"<color={CGM.Core.BattleConstants.ColorGreen}>{currentBlock}</color>";
                }
                else if (modifierBlock < 0)
                {
                    blockStr = $"<color={CGM.Core.BattleConstants.ColorRed}>{currentBlock}</color>";
                }
                result = result.Replace("{B}", blockStr);
            }

            // 3. 处理血糖变化占位符 {G}
            if (result.Contains("{G}"))
            {
                // 计算当前受血糖状态修正后的血糖变化值
                float currentGlucoseChange = glucoseChange * glucoseMultiplier;
                string formattedVal = System.Math.Abs(currentGlucoseChange).ToString("F1");
                
                string glucoseStr;
                // 如果血糖波动倍率大于 1（高血糖 Hyper 状态），强制显示为红色高亮
                if (glucoseMultiplier > 1.0f)
                {
                    glucoseStr = currentGlucoseChange >= 0 
                        ? $"增加 <color={CGM.Core.BattleConstants.ColorRed}>{formattedVal}</color>" 
                        : $"降低 <color={CGM.Core.BattleConstants.ColorRed}>{formattedVal}</color>";
                }
                else
                {
                    // 正常状态：升糖显示橘黄，降糖显示绿色
                    glucoseStr = currentGlucoseChange >= 0 
                        ? $"增加 <color={CGM.Core.BattleConstants.ColorOrange}>{formattedVal}</color>" 
                        : $"降低 <color={CGM.Core.BattleConstants.ColorGreen}>{formattedVal}</color>";
                }
                
                result = result.Replace("{G}", glucoseStr);
            }

            // 4. 处理效果参数占位符 {Ei_Vj}
            if (effects != null)
            {
                for (int i = 0; i < effects.Count; i++)
                {
                    var effect = effects[i];
                    result = result.Replace($"{{E{i}_V1}}", effect.value1);
                    result = result.Replace($"{{E{i}_V2}}", effect.value2);
                }
            }

            // 5. 将 「状态名称」 自动高亮为 TextMeshPro 的链接格式，如 <color=#FFAD1F><link="SlowRelease"><u>缓释</u></link></color>
            foreach (var buff in BuffDatabase.GetAllBuffs())
            {
                string bracketedName = $"「{buff.name}」";
                if (result.Contains(bracketedName))
                {
                    string linkStr = $"「<color={buff.colorHex}><link=\"{buff.id}\"><u>{buff.name}</u></link></color>」";
                    result = result.Replace(bracketedName, linkStr);
                }
            }

            return result;
        }

        /// <summary>
        /// 获取卡牌类型枚举值。

        /// </summary>

        public CardType GetCardType()
        {
            return (CardType)Enum.Parse(typeof(CardType), type);
        }

        /// <summary>
        /// 获取卡牌稀有度枚举值。
        /// </summary>
        public CardRarity GetCardRarity()
        {
            return (CardRarity)Enum.Parse(typeof(CardRarity), rarity);
        }

        /// <summary>
        /// 判断该卡牌是否具有指定类型的效果。
        /// </summary>
        /// <param name="type">要查找的效果类型（CSV snake_case 格式）</param>
        public bool HasEffect(string type)
        {
            foreach (var effect in effects)
            {
                if (effect.effectType == type) return true;
            }
            return false;
        }

        /// <summary>
        /// 获取指定类型的第一个效果，未找到时返回 null。
        /// </summary>
        /// <param name="type">要查找的效果类型（CSV snake_case 格式）</param>
        public CardEffect GetEffect(string type)
        {
            foreach (var effect in effects)
            {
                if (effect.effectType == type) return effect;
            }
            return null;
        }

        /// <summary>
        /// 获取指定类型的所有效果。
        /// </summary>
        /// <param name="type">要查找的效果类型（CSV snake_case 格式）</param>
        public List<CardEffect> GetEffects(string type)
        {
            List<CardEffect> result = new List<CardEffect>();
            foreach (var effect in effects)
            {
                if (effect.effectType == type) result.Add(effect);
            }
            return result;
        }
    }

    /// <summary>
    /// 卡牌数据库包装类，用于 JsonUtility 整体序列化与反序列化。
    /// </summary>
    [Serializable]
    public class CardDataWrapper
    {
        public List<CardInfo> cards = new List<CardInfo>();
    }
}
