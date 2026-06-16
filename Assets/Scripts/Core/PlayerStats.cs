using System;
using UnityEngine;
using CGM.Data;

namespace CGM.Core
{
    /// <summary>
    /// 玩家的特定属性管理组件。
    /// 在 EntityStats 基础上引入血糖值控制以及“缓释”、“敏化”次数盾的处理逻辑。
    /// </summary>
    public class PlayerStats : EntityStats
    {
        [Header("玩家特有配置")]
        [Range(0f, 15f)]
        [SerializeField] private float currentGlucose = 5.5f;

        /// <summary>
        /// 当血糖值发生任何改变时触发，通知血糖仪 UI 刷新。
        /// </summary>
        public event Action<float> OnGlucoseChanged;

        public float CurrentGlucose => currentGlucose;

        protected override void Start()
        {
            base.Start();
            NotifyGlucoseChange();
        }

        /// <summary>
        /// 初始化玩家的生命值与血糖初始值。
        /// </summary>
        public void Initialize(int maxHealth, float startGlucose)
        {
            base.Initialize(maxHealth);
            currentGlucose = Mathf.Clamp(startGlucose, BattleConstants.GlucoseMin, BattleConstants.GlucoseMax);
            NotifyGlucoseChange();
        }

        /// <summary>
        /// 修改当前血糖值，并处理“缓释”与“敏化”被动次数盾的效果。
        /// </summary>
        /// <param name="changeAmount">血糖变化量（正数为升糖，负数为降糖）</param>
        public void ChangeGlucose(float changeAmount)
        {
            if (changeAmount == 0f) return;

            // 1. 升糖判定：如果血糖增加，检查是否有「缓释」层数。
            if (changeAmount > 0f)
            {
                int slowReleaseCount = GetBuffCount(BuffId.SlowRelease);
                if (slowReleaseCount > 0)
                {
                    // 消耗 1 层「缓释」
                    ApplyBuff(BuffId.SlowRelease, -1);
                    // 缓释抵消本次升糖效果，血糖改变量归零
                    changeAmount = 0f;
                    Debug.Log("[PlayerStats] 触发「缓释」效果：本次升糖数值已被成功抵消！");
                }
            }
            // 2. 降糖判定：如果血糖降低，检查是否有「敏化」层数。
            else if (changeAmount < 0f)
            {
                int sensitivityCount = GetBuffCount(BuffId.Sensitivity);
                if (sensitivityCount > 0)
                {
                    // 消耗 1 层「敏化」
                    ApplyBuff(BuffId.Sensitivity, -1);
                    // 降糖效果翻倍
                    changeAmount *= 2f;
                    Debug.Log("[PlayerStats] 触发「敏化」效果：本次降糖数值成功翻倍！");
                }
            }

            // 3. 应用血糖变化，并限制在设计区间
            if (changeAmount != 0f)
            {
                currentGlucose = Mathf.Clamp(currentGlucose + changeAmount, BattleConstants.GlucoseMin, BattleConstants.GlucoseMax);
                NotifyGlucoseChange();
            }
        }

        private void NotifyGlucoseChange()
        {
            OnGlucoseChanged?.Invoke(currentGlucose);
            // 血糖的变化也会连带影响卡牌显示伤害/防守系数的全局状态，因此同样触发基础 Stats 事件
            NotifyChange();
        }
    }
}
