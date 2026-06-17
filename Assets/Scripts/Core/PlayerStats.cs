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
        [SerializeField] private float currentGlucose = 5.7f;
        [SerializeField] private int gold = 99;

        /// <summary>
        /// 当血糖值发生任何改变时触发，通知血糖仪 UI 刷新。
        /// </summary>
        public event Action<float> OnGlucoseChanged;

        /// <summary>
        /// 当金币发生变化时触发，通知金币 UI 刷新。
        /// </summary>
        public event Action<int> OnGoldChanged;

        public float CurrentGlucose => currentGlucose;
        public int Gold => gold;

        protected override void Start()
        {
            base.Start();
            NotifyGlucoseChange();
            OnGoldChanged?.Invoke(gold);
        }

        /// <summary>
        /// 初始化玩家的生命值与血糖、金币初始值。
        /// </summary>
        public void Initialize(int maxHealth, float startGlucose, int startGold = 99)
        {
            base.Initialize(maxHealth);
            currentGlucose = Mathf.Clamp(startGlucose, BattleConstants.GlucoseMin, BattleConstants.GlucoseMax);
            gold = startGold;
            NotifyGlucoseChange();
            OnGoldChanged?.Invoke(gold);
        }

        /// <summary>
        /// 直接设置当前血糖值，不触发缓释/敏化次数盾逻辑。
        /// </summary>
        public void SetGlucose(float value)
        {
            currentGlucose = Mathf.Clamp(value, BattleConstants.GlucoseMin, BattleConstants.GlucoseMax);
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
                SetGlucose(currentGlucose + changeAmount);
            }
        }

        /// <summary>
        /// 修改玩家金币数量，金币最低为0。
        /// </summary>
        public void ChangeGold(int amount)
        {
            gold = Mathf.Max(0, gold + amount);
            OnGoldChanged?.Invoke(gold);
        }

        private void NotifyGlucoseChange()
        {
            OnGlucoseChanged?.Invoke(currentGlucose);
            // 血糖的变化也会连带影响卡牌显示伤害/防守系数的全局状态，因此同样触发基础 Stats 事件
            NotifyChange();
        }
    }
}
