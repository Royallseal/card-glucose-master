// =============================================================================
// EnemyLayoutTest.cs — 敌人加载与意图循环测试脚本
// 命名空间：CGM.Test
// 职责：动态从配置中实例化加载全部 12 种代谢敌人，提供一键推进意图、施加状态测试接口。
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using CGM.Core;
using CGM.Data;
using CGM.UI;

namespace CGM.Test
{
    /// <summary>
    /// 测试敌人动态加载与意图重算的验证脚本。
    /// 挂载于测试容器上即可在运行期一键测试 12 种敌人。
    /// </summary>
    public class EnemyLayoutTest : MonoBehaviour
    {
        [Header("测试配置")]
        [Tooltip("敌人预制体（需挂载有 EnemyStats 与 EnemyUI，若为空则从 Resources 加载 Prefabs/Enemy）")]
        [SerializeField] private GameObject enemyPrefab;

        [Tooltip("生成的挂载容器，若为空则默认使用当前物体")]
        [SerializeField] private Transform container;

        [Tooltip("是否在启动时自动实例化所有 12 种怪物")]
        [SerializeField] private bool autoGenerateOnStart = true;

        private List<EnemyStats> _spawnedEnemies = new List<EnemyStats>();
        private PlayerStats _playerInstance;

        private void Start()
        {
            if (container == null)
            {
                container = transform;
            }

            // 自动查找或生成临时的 PlayerStats 以便提供脆弱/健康血糖测试
            _playerInstance = FindObjectOfType<PlayerStats>();
            if (_playerInstance == null)
            {
                GameObject playerGo = new GameObject("[Temp_PlayerStats]");
                _playerInstance = playerGo.AddComponent<PlayerStats>();
                _playerInstance.Initialize(80, 5.7f); // 初始血量 80，血糖 5.7 (健康区间)
            }

            if (autoGenerateOnStart)
            {
                GenerateAllEnemies();
            }
        }

        /// <summary>
        /// 一键生成数据库中配置的所有 12 种敌人在 UI 列表中呈现。
        /// </summary>
        public void GenerateAllEnemies()
        {
            // 确保 EnemyDatabase 存在并加载
            EnemyDatabase database = FindObjectOfType<EnemyDatabase>();
            if (database == null)
            {
                GameObject dbGo = new GameObject("[Temp_EnemyDatabase]");
                database = dbGo.AddComponent<EnemyDatabase>();
            }

            List<EnemyInfo> allEnemies = database.GetAllEnemies();
            if (allEnemies == null || allEnemies.Count == 0)
            {
                Debug.LogError("[EnemyLayoutTest] 未能在 EnemyDatabase 中获取到任何敌人数据，请确保 enemies.json 已编译。");
                return;
            }

            // 如果预制体为空，尝试从 Resources 加载
            if (enemyPrefab == null)
            {
                enemyPrefab = Resources.Load<GameObject>("Prefabs/Enemy");
            }

            if (enemyPrefab == null)
            {
                Debug.LogError("[EnemyLayoutTest] 未指定敌人预制体，且未能加载 Resources/Prefabs/Enemy。请先在 Inspector 中配置！");
                return;
            }

            // 清理容器中旧的物体
            foreach (Transform child in container)
            {
                Destroy(child.gameObject);
            }
            _spawnedEnemies.Clear();

            Debug.Log($"[EnemyLayoutTest] 开始实例化敌人，共 {allEnemies.Count} 种。");

            foreach (var info in allEnemies)
            {
                GameObject instance = Instantiate(enemyPrefab, container);
                instance.name = $"Enemy_{info.id}";

                EnemyStats stats = instance.GetComponent<EnemyStats>();
                EnemyUI ui = instance.GetComponent<EnemyUI>();

                if (stats == null || ui == null)
                {
                    Debug.LogError($"[EnemyLayoutTest] 敌人预制体 {enemyPrefab.name} 上缺少 EnemyStats 或 EnemyUI 组件！");
                    Destroy(instance);
                    continue;
                }

                // 1. 动态加载敌人属性与意图循环配置
                stats.LoadEnemy(info.id);

                // 2. 绑定 UI 渲染源
                ui.SetEnemy(stats);

                _spawnedEnemies.Add(stats);
            }

            Debug.Log("[EnemyLayoutTest] 12 种代谢敌人动态加载渲染完成。");
        }

        /// <summary>
        /// 一键驱动所有怪物执行当前意图（会模拟对玩家产生伤害、或者自己叠甲/施加 Buff 等）。
        /// </summary>
        public void ExecuteAllEnemyIntents()
        {
            if (_spawnedEnemies.Count == 0) return;

            Debug.Log("<color=#FF6B6B>[EnemyLayoutTest]</color> 触发驱动所有怪物执行当前回合行动：");
            foreach (var enemy in _spawnedEnemies)
            {
                if (enemy != null && enemy.CurrentHp > 0)
                {
                    enemy.ExecuteIntent(_playerInstance);
                }
            }
        }

        /// <summary>
        /// 模拟给玩家施加 1 层「脆弱」状态，这应使所有处于「攻击意图」的怪物伤害自动重算变绿（+50%伤害）。
        /// </summary>
        public void SimulateApplyFragilityToPlayer()
        {
            if (_playerInstance != null)
            {
                _playerInstance.ApplyBuff(BuffId.Fragility, 1);
                Debug.Log("[EnemyLayoutTest] 已为玩家施加 1 层「脆弱」状态，怪物意图重算触发中...");
            }
        }

        /// <summary>
        /// 模拟给所有怪物施加 2 层「活力」状态，这应使怪物当前造成的物理伤害和格挡发生改变。
        /// </summary>
        public void SimulateApplyVitalityToEnemies()
        {
            Debug.Log("[EnemyLayoutTest] 为所有生成的怪物附加 2 层「活力」增益：");
            foreach (var enemy in _spawnedEnemies)
            {
                if (enemy != null)
                {
                    enemy.ApplyBuff(BuffId.Vitality, 2);
                }
            }
        }

        /// <summary>
        /// 模拟给所有怪物施加 1 层「乏力」状态，这应使怪物当前造成的攻击伤害降低（变红）。
        /// </summary>
        public void SimulateApplyLethargyToEnemies()
        {
            Debug.Log("[EnemyLayoutTest] 为所有生成的怪物附加 1 层「乏力」减益：");
            foreach (var enemy in _spawnedEnemies)
            {
                if (enemy != null)
                {
                    enemy.ApplyBuff(BuffId.Lethargy, 1);
                }
            }
        }
    }
}
