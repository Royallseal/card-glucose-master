namespace CGM.Core
{
    /// <summary>
    /// 游戏战斗系统数值与 UI 渲染富文本颜色常量。
    /// 集中管理数值阈值、修正系数与着色，提高代码可读性与可维护性。
    /// </summary>
    public static class BattleConstants
    {
        // =========================================================================
        // UI 富文本颜色编码 (HEX Colors)
        // =========================================================================
        public const string ColorGreen = "#4EC9B0";  // 伤害/格挡增益、血糖下降健康色 (青绿色)
        public const string ColorRed = "#FF6B6B";    // 伤害/格挡减益、高血糖警示红色 (淡红色)
        public const string ColorOrange = "#FFAD1F"; // 血糖上升/状态高亮橙黄色 (橘黄色)
        public const string ColorGold = "#FFD700";   // 金币黄色 (黄色)

        // =========================================================================
        // 血糖阈值与修正比例
        // =========================================================================
        public const float GlucoseDeathMin = 2.0f;   // 低于此值判负
        public const float GlucoseDeathMax = 15.0f;   // 高于此值判负
        public const float GlucoseMin = 2.0f;
        public const float GlucoseMax = 15.0f;

        public const float HealthyGlucoseMin = 4.4f;
        public const float HealthyGlucoseMax = 7.0f;
        public const float HyperGlucoseThreshold = 7.0f;  // >7.0 即为高血糖，与健康区无缝衔接

        public const float HealthyModifierMultiplier = 1.25f; // 健康状态提升 25%
        public const float HyperModifierMultiplier = 0.75f;   // 高血糖状态削减 25%
        public const float HyperGlucoseFluctuationMultiplier = 2.0f; // 高血糖血糖波动翻倍

        // =========================================================================
        // Buff/Debuff 影响数值
        // =========================================================================
        public const float LethargyDamageReduction = 0.25f;  // 乏力：造成伤害降低 25%
        public const float FragilityDamageIncrease = 0.50f;  // 脆弱：受到伤害增加 50%
        public const float StiffnessBlockReduction = 0.25f;  // 僵硬：获得格挡降低 25%
    }
}
