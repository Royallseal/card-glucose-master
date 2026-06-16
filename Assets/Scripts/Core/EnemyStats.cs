using System.Collections.Generic;
using UnityEngine;
using CGM.Data;

namespace CGM.Core
{
    /// <summary>
    /// 敌人实体的属性与行为逻辑管理组件。
    /// 继承自 EntityStats，负责从 EnemyDatabase 加载敌人配置并驱动其意图行动循环。
    /// </summary>
    public class EnemyStats : EntityStats
    {
        [Header("敌人数据配置")]
        [SerializeField] private string enemyId; // 当前敌人的 ID (如 "couch_potato")
        
        private EnemyInfo enemyInfo;
        private List<EnemyIntentInfo> intentCycle = new List<EnemyIntentInfo>();
        private int currentIntentIndex = 0;

        public EnemyInfo EnemyInfo => enemyInfo;
        public string EnemyId => enemyId;

        protected override void Start()
        {
            base.Start();
            
            // 如果在面板上预填了 ID，则自动执行一次加载
            if (!string.IsNullOrEmpty(enemyId))
            {
                LoadEnemy(enemyId);
            }
        }

        /// <summary>
        /// 从数据库中根据 ID 动态加载敌人配置并初始化属性。
        /// </summary>
        public void LoadEnemy(string id)
        {
            enemyId = id;
            
            if (EnemyDatabase.Instance == null)
            {
                Debug.LogError("[EnemyStats] EnemyDatabase 单例未初始化，无法加载敌人数据！");
                return;
            }

            enemyInfo = EnemyDatabase.Instance.GetEnemyById(id);
            if (enemyInfo == null)
            {
                Debug.LogError($"[EnemyStats] 未能在数据库中查找到敌人 ID：{id}");
                return;
            }

            // 1. 初始化生命值与格挡
            Initialize(enemyInfo.maxHp);

            // 2. 解析敌人的意图行动循环模式
            intentCycle = enemyInfo.GetIntentCycle();
            currentIntentIndex = 0;

            Debug.Log($"<color=#4EC9B0>[EnemyStats]</color> 成功加载敌人：<b>{enemyInfo.name}</b>，" +
                      $"最大生命值：<b>{maxHp}</b>，意图循环步数：<b>{intentCycle.Count}</b>");
            
            NotifyChange();
        }

        /// <summary>
        /// 获取敌人当前回合要执行的意图行动。
        /// </summary>
        public EnemyIntentInfo GetCurrentIntent()
        {
            if (intentCycle == null || intentCycle.Count == 0) return null;
            return intentCycle[currentIntentIndex];
        }

        /// <summary>
        /// 步进到行动序列中的下一个意图动作。
        /// </summary>
        public void AdvanceIntent()
        {
            if (intentCycle == null || intentCycle.Count <= 1) return;
            currentIntentIndex = (currentIntentIndex + 1) % intentCycle.Count;
            NotifyChange();
        }

        /// <summary>
        /// 供测试或特殊机制使用，手动跳转到指定意图索引。
        /// </summary>
        public void SetIntentIndex(int index)
        {
            if (intentCycle == null || intentCycle.Count == 0) return;
            currentIntentIndex = Mathf.Clamp(index, 0, intentCycle.Count - 1);
            NotifyChange();
        }
    }
}
