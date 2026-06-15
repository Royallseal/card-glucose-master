// =============================================================================
// CardDatabase.cs — 卡牌数据库运行时管理器
// 命名空间：CGM.Data
// 职责：在游戏启动时从 Resources 加载 JSON 卡牌数据，提供按 ID / 类型查询接口。
// =============================================================================

using System.Collections.Generic;
using UnityEngine;

namespace CGM.Data
{
    /// <summary>
    /// 卡牌数据库运行时单例，负责加载与查询卡牌数据。
    /// </summary>
    public class CardDatabase : MonoBehaviour
    {
        /// <summary>
        /// 全局单例实例。
        /// </summary>
        public static CardDatabase Instance { get; private set; }

        private Dictionary<string, CardInfo> _cardMap = new Dictionary<string, CardInfo>();
        private List<CardInfo> _allCards = new List<CardInfo>();

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

            LoadCardData();
        }

        // =====================================================================
        // 数据加载
        // =====================================================================

        /// <summary>
        /// 从 Resources 目录加载 JSON 卡牌数据并构建查询索引。
        /// </summary>
        private void LoadCardData()
        {
            TextAsset jsonAsset = Resources.Load<TextAsset>("Configs/cards");
            if (jsonAsset == null)
            {
                Debug.LogError("[CardDatabase] 未找到卡牌数据文件 Resources/Configs/cards.json。" +
                               "请先在 Unity 编辑器中执行 Tools -> CGM -> 编译卡牌数据。");
                return;
            }

            CardDataWrapper wrapper = JsonUtility.FromJson<CardDataWrapper>(jsonAsset.text);
            if (wrapper == null || wrapper.cards == null)
            {
                Debug.LogError("[CardDatabase] 卡牌数据反序列化失败，请检查 cards.json 格式是否正确。");
                return;
            }

            _allCards = wrapper.cards;
            _cardMap.Clear();

            foreach (var card in _allCards)
            {
                if (_cardMap.ContainsKey(card.id))
                {
                    Debug.LogWarning($"[CardDatabase] 发现重复卡牌 ID：{card.id}，后者将覆盖前者。");
                }
                _cardMap[card.id] = card;
            }

            Debug.Log($"<color=#4EC9B0>[CardDatabase]</color> 卡牌数据库加载完成，共 <b>{_allCards.Count}</b> 张卡牌。");
        }

        // =====================================================================
        // 公共查询接口
        // =====================================================================

        /// <summary>
        /// 按 ID 精确查找卡牌数据。
        /// </summary>
        /// <param name="id">卡牌唯一标识符（如 "starter_rice"）</param>
        /// <returns>匹配的卡牌数据，未找到时返回 null</returns>
        public CardInfo GetCardById(string id)
        {
            _cardMap.TryGetValue(id, out var card);
            return card;
        }

        /// <summary>
        /// 按卡牌类型筛选全部卡牌。
        /// </summary>
        /// <param name="type">目标卡牌类型</param>
        /// <returns>符合条件的卡牌列表（新建副本，可安全修改）</returns>
        public List<CardInfo> GetCardsByType(CardType type)
        {
            List<CardInfo> result = new List<CardInfo>();
            string typeStr = type.ToString();
            foreach (var card in _allCards)
            {
                if (card.type == typeStr)
                {
                    result.Add(card);
                }
            }
            return result;
        }

        /// <summary>
        /// 按稀有度筛选全部卡牌。
        /// </summary>
        /// <param name="rarity">目标稀有度</param>
        /// <returns>符合条件的卡牌列表</returns>
        public List<CardInfo> GetCardsByRarity(CardRarity rarity)
        {
            List<CardInfo> result = new List<CardInfo>();
            string rarityStr = rarity.ToString();
            foreach (var card in _allCards)
            {
                if (card.rarity == rarityStr)
                {
                    result.Add(card);
                }
            }
            return result;
        }

        /// <summary>
        /// 返回全部卡牌数据。
        /// </summary>
        /// <returns>所有卡牌的列表（新建副本，可安全修改）</returns>
        public List<CardInfo> GetAllCards()
        {
            return new List<CardInfo>(_allCards);
        }

        /// <summary>
        /// 根据卡牌类型和稀有度，返回对应的卡框 Sprite 资源路径（不含扩展名）。
        /// </summary>
        /// <param name="card">卡牌数据</param>
        /// <returns>Resources 下的 Sprite 路径</returns>
        public string GetFrameSpritePath(CardInfo card)
        {
            CardType cardType = card.GetCardType();
            CardRarity cardRarity = card.GetCardRarity();

            // 初始卡使用统一的蓝色卡框
            if (cardType == CardType.Starter)
            {
                return "Sprites/UI/frame_blue_starter";
            }

            // 其他类型根据颜色 + 稀有度拼接路径
            string color;
            switch (cardType)
            {
                case CardType.Diet:     color = "red";    break;
                case CardType.Exercise: color = "green";  break;
                case CardType.Medicine: color = "purple"; break;
                default:                color = "red";    break;
            }

            string rarity = cardRarity.ToString().ToLower();
            return $"Sprites/UI/frame_{color}_{rarity}";
        }
    }
}
