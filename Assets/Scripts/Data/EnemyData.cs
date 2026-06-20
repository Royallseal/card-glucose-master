using System;
using System.Collections.Generic;

namespace CGM.Data
{
    /// <summary>
    /// 敌人单个意图行动的解析数据结构。
    /// </summary>
    [Serializable]
    public class EnemyIntentInfo
    {
        public string actionType; // attack, block, buff, debuff
        public string parameter1; // 数值，或者 BuffId 的字符串 (如 vitality, lethargy)
        public int parameter2;    // 层数/次数参数 (BuffId 时使用)

        /// <summary>
        /// 获取意图数值参数 (如伤害值或格挡值)。
        /// </summary>
        public int GetValue()
        {
            if (int.TryParse(parameter1, out int val))
            {
                return val;
            }
            return 0;
        }
    }

    /// <summary>
    /// 单个敌人的基础配置属性，对应 initial_enemies_data.csv 中的一行。
    /// </summary>
    [Serializable]
    public class EnemyInfo
    {
        public string id;
        public string name;
        public int maxHp;
        public string intentPattern; // 分号分隔的意图循环序列，例如: attack:5;block:5;debuff:stiffness:1
        public string levelName;     // 对应的关卡/场景名字，来自 CSV 配置
        public string description;      // 敌人背景故事介绍
        public string levelDescription; // 场景/关卡背景描述

        /// <summary>
        /// 解析意图字符串，返回结构化的意图行动循环列表。
        /// 支持用 | 分隔多套循环，开局时随机选一套。
        /// </summary>
        public List<EnemyIntentInfo> GetIntentCycle()
        {
            string effectivePattern = intentPattern;

            // 检查是否有多套循环（| 分隔），随机选一套
            if (!string.IsNullOrEmpty(intentPattern) && intentPattern.Contains("|"))
            {
                string[] options = intentPattern.Split('|');
                effectivePattern = options[UnityEngine.Random.Range(0, options.Length)];
            }

            return ParsePattern(effectivePattern);
        }

        private static List<EnemyIntentInfo> ParsePattern(string pattern)
        {
            List<EnemyIntentInfo> cycle = new List<EnemyIntentInfo>();
            if (string.IsNullOrEmpty(pattern)) return cycle;

            string[] intentStrings = pattern.Split(';');
            foreach (var intentStr in intentStrings)
            {
                if (string.IsNullOrEmpty(intentStr)) continue;

                string[] parts = intentStr.Split(':');
                if (parts.Length < 2) continue;

                EnemyIntentInfo intent = new EnemyIntentInfo
                {
                    actionType = parts[0].Trim()
                };

                if (intent.actionType == "attack" || intent.actionType == "block")
                {
                    intent.parameter1 = parts[1].Trim();
                    intent.parameter2 = 0;
                }
                else if (intent.actionType == "buff" || intent.actionType == "debuff")
                {
                    intent.parameter1 = parts[1].Trim();
                    intent.parameter2 = parts.Length > 2 ? int.Parse(parts[2].Trim()) : 1;
                }

                cycle.Add(intent);
            }

            return cycle;
        }
    }

    /// <summary>
    /// 敌人数据库包装类，用于 JsonUtility 的整体反序列化。
    /// </summary>
    [Serializable]
    public class EnemyDataWrapper
    {
        public List<EnemyInfo> enemies = new List<EnemyInfo>();
    }
}
