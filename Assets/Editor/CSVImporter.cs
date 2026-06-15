// =============================================================================
// CSVImporter.cs — 卡牌数据 CSV 编译器（Unity 编辑器扩展）
// 命名空间：CGM.Editor
// 职责：读取 data/ 目录下的两张 CSV 表，合并编译为 JSON 输出至 Resources。
// 触发方式：Unity 菜单栏 Tools -> CGM -> 编译卡牌数据
// =============================================================================

using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using CGM.Data;

namespace CGM.Editor
{
    /// <summary>
    /// 卡牌数据 CSV 编译器，将主表与效果表合并编译为 JSON 文件。
    /// </summary>
    public static class CSVImporter
    {
        private const string MainCsvRelativePath = "data/initial_cards_data.csv";
        private const string EffectsCsvRelativePath = "data/card_effects.csv";
        private const string OutputDirectory = "Assets/Resources/Configs";
        private const string OutputFilePath = "Assets/Resources/Configs/cards.json";

        /// <summary>
        /// 编译卡牌数据的菜单入口。
        /// </summary>
        [MenuItem("Tools/CGM/编译卡牌数据")]
        public static void ImportCardData()
        {
            // Application.dataPath 指向 Assets 目录，其父目录为项目根目录
            string projectRoot = Path.GetDirectoryName(Application.dataPath);

            string mainCsvPath = Path.Combine(projectRoot, MainCsvRelativePath);
            string effectsCsvPath = Path.Combine(projectRoot, EffectsCsvRelativePath);

            // 校验文件存在性
            if (!File.Exists(mainCsvPath))
            {
                Debug.LogError($"[CSVImporter] 主表文件未找到：{mainCsvPath}");
                return;
            }
            if (!File.Exists(effectsCsvPath))
            {
                Debug.LogError($"[CSVImporter] 效果表文件未找到：{effectsCsvPath}");
                return;
            }

            // 第一步：解析效果表，按 cardId 分组
            Dictionary<string, List<CardEffect>> effectsMap = ParseEffectsCSV(effectsCsvPath);

            // 第二步：解析主表
            List<CardInfo> cards = ParseMainCSV(mainCsvPath);

            // 第三步：将效果挂载到对应卡牌
            int totalEffects = 0;
            foreach (var card in cards)
            {
                if (effectsMap.TryGetValue(card.id, out var effects))
                {
                    card.effects = effects;
                    totalEffects += effects.Count;
                }
            }

            // 第四步：序列化为 JSON
            CardDataWrapper wrapper = new CardDataWrapper { cards = cards };
            string json = JsonUtility.ToJson(wrapper, true);

            // 清理 JSON 中的浮点精度问题，将 "glucoseChange": 0.10000000149011612 优化为 "glucoseChange": 0.1
            json = System.Text.RegularExpressions.Regex.Replace(json, @"(""glucoseChange"":\s*)(-?\d+\.\d+)", m =>
            {
                string key = m.Groups[1].Value;
                string valStr = m.Groups[2].Value;
                if (float.TryParse(valStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float val))
                {
                    // 四舍五入并保留一位小数，使用 InvariantCulture 确保生成 . 逗号
                    double rounded = System.Math.Round((double)val, 1);
                    return $"{key}{rounded.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)}";
                }
                return m.Value;
            });

            string outputFullPath = Path.Combine(projectRoot, OutputFilePath);
            string outputDirFullPath = Path.Combine(projectRoot, OutputDirectory);

            if (!Directory.Exists(outputDirFullPath))
            {
                Directory.CreateDirectory(outputDirFullPath);
            }

            File.WriteAllText(outputFullPath, json, Encoding.UTF8);
            AssetDatabase.Refresh();

            Debug.Log($"<color=#4EC9B0>[CSVImporter]</color> 卡牌数据编译完成：共 <b>{cards.Count}</b> 张卡牌，<b>{totalEffects}</b> 条效果。输出至 {OutputFilePath}");
        }

        // =====================================================================
        // 主表解析
        // =====================================================================

        /// <summary>
        /// 解析主表 CSV 文件，跳过设计专用列（damageWeight, blockWeight, effectVPCost）。
        /// </summary>
        /// <param name="path">主表 CSV 文件的绝对路径</param>
        private static List<CardInfo> ParseMainCSV(string path)
        {
            List<CardInfo> cards = new List<CardInfo>();
            string content = File.ReadAllText(path, Encoding.UTF8);
            string[] lines = content.Split('\n');

            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                List<string> fields = ParseCSVLine(line);

                // 主表列定义（共 12 列）：
                // [0]id [1]name [2]type [3]rarity [4]energyCost
                // [5]glucoseChange [6]damageWeight* [7]blockWeight* [8]effectVPCost*
                // [9]finalDamage [10]finalBlock [11]description
                // 带 * 号的列为设计专用，解析时跳过
                if (fields.Count < 12)
                {
                    Debug.LogWarning($"[CSVImporter] 主表第 {i + 1} 行字段数不足（期望 12，实际 {fields.Count}），已跳过：{line}");
                    continue;
                }

                try
                {
                    CardInfo card = new CardInfo
                    {
                        id = fields[0].Trim(),
                        name = fields[1].Trim(),
                        type = fields[2].Trim(),
                        rarity = fields[3].Trim(),
                        energyCost = int.Parse(fields[4].Trim()),
                        glucoseChange = float.Parse(fields[5].Trim()),
                        // 跳过 [6] damageWeight, [7] blockWeight, [8] effectVPCost
                        finalDamage = int.Parse(fields[9].Trim()),
                        finalBlock = int.Parse(fields[10].Trim()),
                        description = fields[11].Trim()
                    };

                    cards.Add(card);
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[CSVImporter] 主表第 {i + 1} 行解析失败：{e.Message}\n原始行：{line}");
                }
            }

            return cards;
        }

        // =====================================================================
        // 效果表解析
        // =====================================================================

        /// <summary>
        /// 解析效果表 CSV 文件，按 cardId 进行分组。
        /// </summary>
        /// <param name="path">效果表 CSV 文件的绝对路径</param>
        private static Dictionary<string, List<CardEffect>> ParseEffectsCSV(string path)
        {
            var map = new Dictionary<string, List<CardEffect>>();
            string content = File.ReadAllText(path, Encoding.UTF8);
            string[] lines = content.Split('\n');

            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                string[] fields = line.Split(',');

                // 效果表列定义（共 4 列）：
                // [0]cardId [1]effectType [2]value1 [3]value2（可为空）
                if (fields.Length < 3)
                {
                    Debug.LogWarning($"[CSVImporter] 效果表第 {i + 1} 行字段不足，已跳过：{line}");
                    continue;
                }

                string cardId = fields[0].Trim();
                CardEffect effect = new CardEffect
                {
                    effectType = fields[1].Trim(),
                    value1 = fields.Length > 2 ? fields[2].Trim() : "",
                    value2 = fields.Length > 3 ? fields[3].Trim() : ""
                };

                if (!map.ContainsKey(cardId))
                {
                    map[cardId] = new List<CardEffect>();
                }
                map[cardId].Add(effect);
            }

            return map;
        }

        // =====================================================================
        // CSV 行解析器（引号感知）
        // =====================================================================

        /// <summary>
        /// 解析单行 CSV 文本，正确处理双引号包裹的含英文逗号字段。
        /// </summary>
        /// <param name="line">CSV 行文本</param>
        private static List<string> ParseCSVLine(string line)
        {
            List<string> fields = new List<string>();
            bool inQuotes = false;
            StringBuilder current = new StringBuilder();

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    // 处理转义双引号（""）
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    fields.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(c);
                }
            }

            // 添加最后一个字段
            fields.Add(current.ToString());
            return fields;
        }
    }
}
