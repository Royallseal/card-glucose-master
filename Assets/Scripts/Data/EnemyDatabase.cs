using System.Collections.Generic;
using UnityEngine;

namespace CGM.Data
{
    /// <summary>
    /// 敌人配置数据库运行时管理器（单例）。
    /// 在游戏启动时从 Resources 加载 JSON 敌人数据，提供按 ID 查询与随机生成接口。
    /// </summary>
    public class EnemyDatabase : MonoBehaviour
    {
        /// <summary>
        /// 全局单例实例。
        /// </summary>
        public static EnemyDatabase Instance { get; private set; }

        private readonly Dictionary<string, EnemyInfo> _enemyMap = new Dictionary<string, EnemyInfo>();
        private List<EnemyInfo> _allEnemies = new List<EnemyInfo>();

        private void Awake()
        {
            // 单例初始化与防重复
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            LoadEnemyData();
        }

        /// <summary>
        /// 从 Resources 目录加载 JSON 敌人数据并构建查询索引。
        /// </summary>
        private void LoadEnemyData()
        {
            TextAsset jsonAsset = Resources.Load<TextAsset>("Configs/enemies");
            if (jsonAsset == null)
            {
                Debug.LogError("[EnemyDatabase] 未找到敌人数据文件 Resources/Configs/enemies.json。" +
                               "请先在 Unity 编辑器中执行 Tools -> CGM -> 编译敌人数据。");
                return;
            }

            EnemyDataWrapper wrapper = JsonUtility.FromJson<EnemyDataWrapper>(jsonAsset.text);
            if (wrapper == null || wrapper.enemies == null)
            {
                Debug.LogError("[EnemyDatabase] 敌人数据反序列化失败，请检查 enemies.json 格式是否正确。");
                return;
            }

            _allEnemies = wrapper.enemies;
            _enemyMap.Clear();

            foreach (var enemy in _allEnemies)
            {
                if (enemy.description != null)
                {
                    enemy.description = enemy.description.Replace("\\n", "\n");
                }
                if (enemy.levelDescription != null)
                {
                    enemy.levelDescription = enemy.levelDescription.Replace("\\n", "\n");
                }
                if (_enemyMap.ContainsKey(enemy.id))
                {
                    Debug.LogWarning($"[EnemyDatabase] 发现重复敌人 ID：{enemy.id}，后者将覆盖前者。");
                }
                _enemyMap[enemy.id] = enemy;
            }

            Debug.Log($"<color=#4EC9B0>[EnemyDatabase]</color> 敌人数据库加载完成，共 <b>{_allEnemies.Count}</b> 种敌人。");
        }

        /// <summary>
        /// 按 ID 精确查找敌人配置数据。
        /// </summary>
        public EnemyInfo GetEnemyById(string id)
        {
            _enemyMap.TryGetValue(id, out var enemy);
            return enemy;
        }

        /// <summary>
        /// 返回全部敌人数据列表的副本。
        /// </summary>
        public List<EnemyInfo> GetAllEnemies()
        {
            return new List<EnemyInfo>(_allEnemies);
        }

        /// <summary>
        /// 随机获取一个敌人数据（用于关卡敌人生成测试）。
        /// </summary>
        public EnemyInfo GetRandomEnemy()
        {
            if (_allEnemies == null || _allEnemies.Count == 0) return null;
            int randomIndex = Random.Range(0, _allEnemies.Count);
            return _allEnemies[randomIndex];
        }
    }
}
