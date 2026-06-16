using UnityEngine;
using CGM.Data;

namespace CGM.Core
{
    /// <summary>
    /// 核心战斗数值结算与血糖修正计算器。
    /// 封装了各种独立状态的数值增量/倍率获取方法，严格遵循 design_card_formula.md 中的结算优先级和零值保底逻辑。
    /// </summary>
    public static class BattleCalculator
    {
        // =========================================================================
        // 1. 状态数值影响封装 (Encapsulated State Modifiers)
        // =========================================================================

        /// <summary>
        /// 获取当前实体的活力伤害加成值（由活力状态层数决定）。
        /// </summary>
        public static int GetVitalityDamageBonus(EntityStats source)
        {
            if (source == null) return 0;
            return source.GetBuffCount(BuffId.Vitality);
        }

        /// <summary>
        /// 获取当前实体的耐力格挡加成值（由耐力状态层数决定）。
        /// </summary>
        public static int GetEnduranceBlockBonus(EntityStats source)
        {
            if (source == null) return 0;
            return source.GetBuffCount(BuffId.Endurance);
        }

        /// <summary>
        /// 获取攻击方的乏力状态伤害削减乘数（乏力状态造成伤害 -25%）。
        /// </summary>
        public static float GetLethargyDamageMultiplier(EntityStats source)
        {
            if (source == null) return 1.0f;
            return source.GetBuffCount(BuffId.Lethargy) > 0 
                ? (1.0f - BattleConstants.LethargyDamageReduction) 
                : 1.0f;
        }

        /// <summary>
        /// 获取防守方的僵硬状态格挡削减乘数（僵硬状态获得格挡 -25%）。
        /// </summary>
        public static float GetStiffnessBlockMultiplier(EntityStats source)
        {
            if (source == null) return 1.0f;
            return source.GetBuffCount(BuffId.Stiffness) > 0 
                ? (1.0f - BattleConstants.StiffnessBlockReduction) 
                : 1.0f;
        }

        /// <summary>
        /// 获取被攻击方的脆弱状态伤害增加乘数（脆弱状态受击伤害 +50%）。
        /// </summary>
        public static float GetFragilityDamageMultiplier(EntityStats target)
        {
            if (target == null) return 1.0f;
            return target.GetBuffCount(BuffId.Fragility) > 0 
                ? (1.0f + BattleConstants.FragilityDamageIncrease) 
                : 1.0f;
        }

        /// <summary>
        /// 根据实体当前的血糖值获取全局血糖伤害/格挡修正乘数。
        /// </summary>
        public static float GetGlucoseMultiplier(EntityStats entity)
        {
            if (entity is PlayerStats player)
            {
                float glucose = player.CurrentGlucose;
                if (glucose >= BattleConstants.HealthyGlucoseMin && glucose <= BattleConstants.HealthyGlucoseMax)
                {
                    return BattleConstants.HealthyModifierMultiplier; // 健康提升 25% (1.25)
                }
                else if (glucose >= BattleConstants.HyperGlucoseThreshold)
                {
                    return BattleConstants.HyperModifierMultiplier;   // 高血糖削减 25% (0.75)
                }
            }
            return 1.0f; // 低血糖或敌人无血糖修正 (1.0)
        }

        /// <summary>
        /// 获取高血糖状态下的血糖波动倍率修正（高血糖波动翻倍 2.0，正常 1.0）。
        /// </summary>
        public static float GetGlucoseChangeMultiplier(PlayerStats player)
        {
            if (player != null && player.CurrentGlucose >= BattleConstants.HyperGlucoseThreshold)
            {
                return BattleConstants.HyperGlucoseFluctuationMultiplier; // 高血糖波动翻倍 (2.0)
            }
            return 1.0f; // 正常波动 (1.0)
        }

        // =========================================================================
        // 2. 核心战斗数据计算接口 (Core Battle Formulas)
        // =========================================================================

        /// <summary>
        /// 计算卡牌作用于目标实体时的最终伤害数值。
        /// 优先级：1. 基础值 + 活力加成 -> 2. 乏力与脆弱乘数修正 -> 3. 血糖乘数修正 -> 4. 向上取整与零值保底。
        /// </summary>
        public static int CalculateDamage(CardInfo card, EntityStats source, EntityStats target)
        {
            if (card.finalDamage <= 0) return 0;

            // 1. 基础值与活力加成
            int baseDamage = card.finalDamage + GetVitalityDamageBonus(source);
            baseDamage = Mathf.Max(0, baseDamage);

            // 2. 状态比例修正 (乏力 x 脆弱)
            float multiplier = GetLethargyDamageMultiplier(source) * GetFragilityDamageMultiplier(target);

            // 3. 自身血糖区间修正
            multiplier *= GetGlucoseMultiplier(source);

            // 4. 最终结算（向上取整与最低 0 保底）
            int finalDamage = Mathf.CeilToInt(baseDamage * multiplier);
            return Mathf.Max(0, finalDamage);
        }

        /// <summary>
        /// 计算卡牌打出时的最终格挡数值。
        /// 优先级：1. 基础值 + 耐力加成 -> 2. 僵硬乘数修正 -> 3. 自身血糖乘数修正 -> 4. 向上取整与零值保底。
        /// </summary>
        public static int CalculateBlock(CardInfo card, EntityStats source)
        {
            if (card.finalBlock <= 0) return 0;

            // 1. 基础值与耐力加成
            int baseBlock = card.finalBlock + GetEnduranceBlockBonus(source);
            baseBlock = Mathf.Max(0, baseBlock);

            // 2. 状态比例修正 (僵硬)
            float multiplier = GetStiffnessBlockMultiplier(source);

            // 3. 自身血糖区间修正
            multiplier *= GetGlucoseMultiplier(source);

            // 4. 最终结算（向上取整与最低 0 保底）
            int finalBlock = Mathf.CeilToInt(baseBlock * multiplier);
            return Mathf.Max(0, finalBlock);
        }

        /// <summary>
        /// 计算卡牌对玩家造成的最终血糖变化值（考虑高血糖波动翻倍）。
        /// </summary>
        public static float CalculateGlucoseChange(CardInfo card, PlayerStats player)
        {
            float baseChange = card.glucoseChange;
            if (baseChange == 0f) return 0f;

            float multiplier = GetGlucoseChangeMultiplier(player);
            return baseChange * multiplier;
        }
    }
}
