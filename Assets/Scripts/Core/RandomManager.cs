using System.Collections.Generic;
using UnityEngine;
using CGM.Data;

namespace CGM.Core
{
    /// <summary>
    /// 全局随机数管理与奖励生成器。
    /// 集中管理卡组洗牌、概率分支、以及打败敌人后的卡牌奖励生成（三选一）。
    /// </summary>
    public static class RandomManager
    {
        private static System.Random localRandom = new System.Random();

        /// <summary>
        /// 使用 Fisher-Yates 算法对列表进行就地洗牌。
        /// </summary>
        public static void Shuffle<T>(List<T> list)
        {
            if (list == null || list.Count <= 1) return;

            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = localRandom.Next(n + 1);
                T value = list[k];
                list[k] = list[n];
                list[n] = value;
            }
        }

        /// <summary>
        /// 随机生成战斗胜利后的卡牌奖励列表（默认生成三选一，剔除初始 Starter 卡）。
        /// </summary>
        /// <param name="rewardCount">生成卡牌数量（默认 3 张）</param>
        /// <returns>符合条件的随机卡牌数据列表</returns>
        public static List<CardInfo> GetRandomRewardCards(int rewardCount = 3)
        {
            List<CardInfo> rewardList = new List<CardInfo>();

            // 确认卡牌数据库是否可用
            if (CardDatabase.Instance == null)
            {
                Debug.LogError("[RandomManager] CardDatabase 实例尚未初始化，无法生成卡牌奖励。");
                return rewardList;
            }

            List<CardInfo> allCards = CardDatabase.Instance.GetAllCards();
            if (allCards == null || allCards.Count == 0)
            {
                Debug.LogError("[RandomManager] 数据库中没有卡牌数据！");
                return rewardList;
            }

            // 1. 过滤候选卡牌：排查掉初始 Starter 卡，只保留 膳食卡 (Diet)、运动卡 (Exercise) 和 药物卡 (Medicine)
            List<CardInfo> candidates = new List<CardInfo>();
            foreach (var card in allCards)
            {
                if (card.GetCardType() != CardType.Starter)
                {
                    candidates.Add(card);
                }
            }

            if (candidates.Count == 0)
            {
                Debug.LogWarning("[RandomManager] 没有找到任何非初始卡牌，将退回使用所有卡牌作为奖励池。");
                candidates = allCards;
            }

            // 2. 随机挑选指定数量的非重复卡牌
            int actualCount = Mathf.Min(rewardCount, candidates.Count);
            List<int> chosenIndices = new List<int>();

            while (chosenIndices.Count < actualCount)
            {
                int randIndex = localRandom.Next(candidates.Count);
                if (!chosenIndices.Contains(randIndex))
                {
                    chosenIndices.Add(randIndex);
                    rewardList.Add(candidates[randIndex]);
                }
            }

            return rewardList;
        }

        /// <summary>
        /// 返回一个 0 到 maxExclusive 之间的随机整数。
        /// </summary>
        public static int Range(int min, int maxExclusive)
        {
            return localRandom.Next(min, maxExclusive);
        }

        /// <summary>
        /// 返回一个 0.0 到 1.0 之间的随机浮点数。
        /// </summary>
        public static float NextFloat()
        {
            return (float)localRandom.NextDouble();
        }
    }
}
