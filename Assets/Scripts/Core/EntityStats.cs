using System;
using System.Collections.Generic;
using UnityEngine;
using CGM.Data;

namespace CGM.Core
{
    /// <summary>
    /// 实体（玩家与敌人）属性与状态的基础管理类。
    /// 负责管理生命值、格挡值、Buff/Debuff 叠加与回合结算。
    /// </summary>
    public class EntityStats : MonoBehaviour
    {
        [Header("基础数值配置")]
        [SerializeField] protected int maxHp = 80;
        [SerializeField] protected int currentHp = 80;
        [SerializeField] protected int block = 0;

        [Header("锁定视觉指示器")]
        [Tooltip("锁定状态下显示的四角高亮框 GameObject")]
        [SerializeField] private GameObject targetIndicator;

        // 存储当前实体身上的所有状态层数（层数允许为负值，如 -3 活力）
        private readonly Dictionary<BuffId, int> activeBuffs = new Dictionary<BuffId, int>();

        /// <summary>
        /// 当属性（HP、格挡、Buff）发生任何改变时触发，通知 UI 刷新。
        /// </summary>
        public event Action OnStatsChanged;

        public int MaxHp => maxHp;
        public int CurrentHp => currentHp;
        public int Block => block;
        public bool IsDead => currentHp <= 0;

        protected virtual void Start()
        {
            // 确保初始血量不溢出
            currentHp = Mathf.Clamp(currentHp, 0, maxHp);
            ShowTargetIndicator(false);
            NotifyChange();
        }

        /// <summary>
        /// 初始化实体属性值。
        /// </summary>
        public virtual void Initialize(int maxHealth)
        {
            maxHp = maxHealth;
            currentHp = maxHealth;
            block = 0;
            activeBuffs.Clear();
            ShowTargetIndicator(false);
            NotifyChange();
        }

        /// <summary>
        /// 实体受击扣血结算。
        /// 优先抵扣格挡值，超出部分扣除生命值。
        /// </summary>
        /// <param name="damageAmount">受到的伤害数值</param>
        public virtual void TakeDamage(int damageAmount)
        {
            if (damageAmount <= 0) return;

            if (block > 0)
            {
                if (block >= damageAmount)
                {
                    block -= damageAmount;
                    damageAmount = 0;
                }
                else
                {
                    damageAmount -= block;
                    block = 0;
                }
            }

            if (damageAmount > 0)
            {
                currentHp = Mathf.Max(0, currentHp - damageAmount);
            }

            NotifyChange();
        }

        /// <summary>
        /// 直接扣除生命值，不受格挡影响。
        /// </summary>
        public virtual void LoseHp(int amount)
        {
            if (amount <= 0) return;

            currentHp = Mathf.Max(0, currentHp - amount);
            NotifyChange();
        }

        /// <summary>
        /// 获得格挡值。
        /// </summary>
        public virtual void GainBlock(int amount)
        {
            if (amount <= 0) return;
            block += amount;
            NotifyChange();
        }

        /// <summary>
        /// 清除当前所有格挡值（通常在回合开始/结束时调用）。
        /// </summary>
        public virtual void ClearBlock()
        {
            if (block != 0)
            {
                block = 0;
                NotifyChange();
            }
        }

        /// <summary>
        /// 施加状态 Buff 或 Debuff。
        /// </summary>
        public virtual void ApplyBuff(BuffId id, int count)
        {
            if (count == 0) return;

            if (activeBuffs.ContainsKey(id))
            {
                activeBuffs[id] += count;
            }
            else
            {
                activeBuffs[id] = count;
            }

            // 如果层数归零，则直接移除状态条目
            if (activeBuffs[id] == 0)
            {
                activeBuffs.Remove(id);
            }

            NotifyChange();
        }

        /// <summary>
        /// 获取指定状态的层数，未持有则返回 0。
        /// </summary>
        public int GetBuffCount(BuffId id)
        {
            if (activeBuffs.TryGetValue(id, out int count))
            {
                return count;
            }
            return 0;
        }

        /// <summary>
        /// 获取当前实体拥有的所有非零状态列表，用于 UI Buff 图标展示。
        /// </summary>
        public Dictionary<BuffId, int> GetAllActiveBuffs()
        {
            return new Dictionary<BuffId, int>(activeBuffs);
        }

        /// <summary>
        /// 回合结束时，衰减单回合消耗性 Buff/Debuff。
        /// 根据设计：脆弱 (Fragility)、乏力 (Lethargy)、僵硬 (Stiffness) 每回合结束层数 -1。
        /// </summary>
        public virtual void TickBuffsEndOfTurn()
        {
            List<BuffId> buffsToDecrement = new List<BuffId> { BuffId.Fragility, BuffId.Lethargy, BuffId.Stiffness };
            bool changed = false;

            foreach (var buffId in buffsToDecrement)
            {
                if (activeBuffs.ContainsKey(buffId))
                {
                    int currentCount = activeBuffs[buffId];
                    if (currentCount > 0)
                    {
                        activeBuffs[buffId] = currentCount - 1;
                        if (activeBuffs[buffId] <= 0)
                        {
                            activeBuffs.Remove(buffId);
                        }
                        changed = true;
                    }
                }
            }

            if (changed)
            {
                NotifyChange();
            }
        }

        /// <summary>
        /// 清除当前实体身上的所有 Buff/Debuff 状态。
        /// </summary>
        public void ClearAllBuffs()
        {
            if (activeBuffs.Count > 0)
            {
                activeBuffs.Clear();
                NotifyChange();
            }
        }

        /// <summary>
        /// 开启或隐藏锁定指示器 UI。
        /// </summary>
        /// <param name="show">是否显示</param>
        public void ShowTargetIndicator(bool show)
        {
            if (targetIndicator != null)
            {
                targetIndicator.SetActive(show);
            }
        }

        /// <summary>
        /// 触发改变事件，广播给绑定的 UI 观察者。
        /// </summary>
        protected void NotifyChange()
        {
            OnStatsChanged?.Invoke();
        }
    }
}
