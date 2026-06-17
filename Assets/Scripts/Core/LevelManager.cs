using System;
using System.Collections.Generic;
using UnityEngine;
using CGM.Data;


namespace CGM.Core
{
    public enum LevelType
    {
        Enemy,
        Shop,
        Boss
    }

    [System.Serializable]
    public class LevelNode
    {
        public int number; // 关卡序号，1 ~ 12
        public LevelType type;
        public string enemyId;
        public string levelName;
    }

    [System.Serializable]
    public class LevelConfigData
    {
        public int layer;
        public List<string> weakEnemyIds;
        public List<string> strongEnemyIds;
        public string bossId;
    }

    [System.Serializable]
    public class LevelConfigWrapper
    {
        public List<LevelConfigData> layers;
    }

    /// <summary>
    /// 关卡管理器，负责开局随机生成 12 关的过关流，并管理当前关卡的流转。
    /// </summary>
    public class LevelManager : MonoBehaviour
    {
        public static LevelManager Instance { get; private set; }

        [Header("关卡序列状态")]
        [SerializeField] private List<LevelNode> levelSequence = new List<LevelNode>();
        [SerializeField] private int currentLevelIndex = 0; // 0 ~ 11

        /// <summary>
        /// 关卡流转改变时触发
        /// </summary>
        public event Action OnLevelChanged;

        public IReadOnlyList<LevelNode> LevelSequence => levelSequence;
        public int CurrentLevelIndex => currentLevelIndex;

        public LevelNode CurrentNode => (currentLevelIndex >= 0 && currentLevelIndex < levelSequence.Count) ? levelSequence[currentLevelIndex] : null;
        public LevelNode NextNode => (currentLevelIndex + 1 >= 0 && currentLevelIndex + 1 < levelSequence.Count) ? levelSequence[currentLevelIndex + 1] : null;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            GenerateLevelSequence();
        }

        /// <summary>
        /// 随机生成 12 关的游戏序列
        /// </summary>
        public void GenerateLevelSequence()
        {
            levelSequence.Clear();
            currentLevelIndex = 0;

            // 一层弱怪、强怪、Boss 池
            List<string> layer1Weak = new List<string> { "fried_chicken", "milk_tea_ghost" };
            List<string> layer1Strong = new List<string> { "couch_potato", "stay_up_bat", "snack_demon" };
            string layer1Boss = "fat_lord";

            // 二层弱怪、强怪、Boss 池
            List<string> layer2Weak = new List<string> { "numb_assassin", "floater_phantom" };
            List<string> layer2Strong = new List<string> { "acid_storm", "hardened_giant", "high_sugar_golem" };
            string layer2Boss = "general_complication";

            // --- 一层 ---
            // 1~2关：一层弱怪随机乱序
            ShuffleList(layer1Weak);
            AddEnemyNode(1, layer1Weak[0]);
            AddEnemyNode(2, layer1Weak[1]);

            // 3~4关：一层强怪选 2 个，随机乱序
            ShuffleList(layer1Strong);
            AddEnemyNode(3, layer1Strong[0]);
            AddEnemyNode(4, layer1Strong[1]);

            // 5关：第一个商店
            AddShopNode(5);

            // 6关：一层 Boss
            AddBossNode(6, layer1Boss);

            // --- 二层 ---
            // 7~8关：二层弱怪随机乱序
            ShuffleList(layer2Weak);
            AddEnemyNode(7, layer2Weak[0]);
            AddEnemyNode(8, layer2Weak[1]);

            // 9~10关：二层强怪选 2 个，随机乱序
            ShuffleList(layer2Strong);
            AddEnemyNode(9, layer2Strong[0]);
            AddEnemyNode(10, layer2Strong[1]);

            // 11关：第二个商店
            AddShopNode(11);

            // 12关：二层 Boss
            AddBossNode(12, layer2Boss);

            Debug.Log($"[LevelManager] 关卡序列生成成功。当前关卡：{CurrentNode?.levelName}");
        }

        /// <summary>
        /// 推进到下一关
        /// </summary>
        public void EnterNextLevel()
        {
            if (currentLevelIndex < levelSequence.Count - 1)
            {
                currentLevelIndex++;
                OnLevelChanged?.Invoke();
                Debug.Log($"[LevelManager] 进入下一关: 关卡 {CurrentNode.number} ({CurrentNode.levelName})");
            }
            else
            {
                Debug.Log("[LevelManager] 已经达到最后一关，通关成功！");
            }
        }

        /// <summary>
        /// 重置关卡状态
        /// </summary>
        public void ResetGame()
        {
            GenerateLevelSequence();
            OnLevelChanged?.Invoke();
        }

        private void AddEnemyNode(int number, string enemyId)
        {
            levelSequence.Add(new LevelNode
            {
                number = number,
                type = LevelType.Enemy,
                enemyId = enemyId,
                levelName = GetLevelNameForEnemy(enemyId)
            });
        }

        private void AddShopNode(int number)
        {
            levelSequence.Add(new LevelNode
            {
                number = number,
                type = LevelType.Shop,
                enemyId = "shop",
                levelName = "商店"
            });
        }

        private void AddBossNode(int number, string bossId)
        {
            levelSequence.Add(new LevelNode
            {
                number = number,
                type = LevelType.Boss,
                enemyId = bossId,
                levelName = GetLevelNameForEnemy(bossId)
            });
        }

        private string GetLevelNameForEnemy(string enemyId)
        {
            EnemyDatabase db = EnemyDatabase.Instance;
            if (db == null)
            {
                db = FindObjectOfType<EnemyDatabase>();
            }

            if (db != null)
            {
                var enemy = db.GetEnemyById(enemyId);
                if (enemy != null && !string.IsNullOrEmpty(enemy.levelName))
                {
                    return enemy.levelName;
                }
            }

            switch (enemyId)
            {
                case "fried_chicken": return "油锅地狱";
                case "milk_tea_ghost": return "甜蜜幻境";
                case "couch_potato": return "惰性沼泽";
                case "stay_up_bat": return "无眠荒野";
                case "snack_demon": return "深夜回廊";
                case "fat_lord": return "脂质王座";
                case "numb_assassin": return "冰寒防线";
                case "floater_phantom": return "重影深渊";
                case "acid_storm": return "失衡血海";
                case "hardened_giant": return "凝固森林";
                case "high_sugar_golem": return "迷失禁区";
                case "general_complication": return "致命风眼";
                default: return "未知关卡";
            }
        }

        private void ShuffleList<T>(List<T> list)
        {
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = UnityEngine.Random.Range(0, n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }
    }
}
