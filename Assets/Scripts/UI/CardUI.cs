// =============================================================================
// CardUI.cs — 卡牌 UI 表现层控制器
// 命名空间：CGM.UI
// 职责：根据 CardInfo 数据动态渲染卡牌的各种视觉元素（卡框、插画、费用、名字、描述）。
// =============================================================================

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CGM.Data;

namespace CGM.UI
{
    public class CardUI : MonoBehaviour
    {
        [Header("UI 核心引用")]
        [SerializeField] private Image frameImage;        // 卡牌外框
        [SerializeField] private Image iconImage;         // 卡牌中央插画
        [SerializeField] private TextMeshProUGUI costText;// 费用
        [SerializeField] private TextMeshProUGUI nameText;// 名称
        [SerializeField] private TextMeshProUGUI descText;// 动态描述
        [SerializeField] private TextMeshProUGUI typeText;// 卡牌类型与稀有度

        /// <summary>
        /// 基于卡牌数据渲染 UI。
        /// </summary>
        /// <param name="card">卡牌数据</param>
        /// <param name="damageModifier">运行时攻击修正值</param>
        /// <param name="blockModifier">运行时格挡修正值</param>
        public void SetCard(CardInfo card, int damageModifier = 0, int blockModifier = 0)
        {
            if (card == null) return;

            // 1. 设置卡牌外边框
            string framePath = CardDatabase.Instance != null 
                ? CardDatabase.Instance.GetFrameSpritePath(card) 
                : GetDefaultFramePath(card);
            
            Sprite frameSprite = Resources.Load<Sprite>(framePath);
            if (frameSprite != null)
            {
                frameImage.sprite = frameSprite;
            }
            else
            {
                Debug.LogWarning($"[CardUI] 未能加载卡框：{framePath}");
            }

            // 2. 设置卡牌中央插画（Icon）
            string iconPath = $"Sprites/Cards/Icons/{card.id}";
            Sprite iconSprite = Resources.Load<Sprite>(iconPath);
            if (iconSprite != null)
            {
                iconImage.sprite = iconSprite;
                iconImage.gameObject.SetActive(true);
                // 设置卡面本身颜色
                if (CardDatabase.Instance != null)
                {
                    iconImage.color = CardDatabase.Instance.GetCardIconColor(card.id);
                }
                else
                {
                    iconImage.color = Color.white;
                }
            }
            else
            {
                iconImage.gameObject.SetActive(false);
                Debug.LogWarning($"[CardUI] 未能加载插画：{iconPath}");
            }

            // 4. 设置文字内容
            costText.text = card.energyCost.ToString();
            nameText.text = card.name;
            descText.text = card.GetDynamicDescription(damageModifier, blockModifier);

            // 5. 设置类型与稀有度文案
            string typeChinese = GetCardTypeChinese(card.GetCardType());
            string rarityChinese = GetCardRarityChinese(card.GetCardRarity());
            typeText.text = $"{typeChinese} • {rarityChinese}";
        }

        private string GetDefaultFramePath(CardInfo card)
        {
            CardType cardType = card.GetCardType();
            CardRarity cardRarity = card.GetCardRarity();

            if (cardType == CardType.Starter) return "Sprites/Cards/frames/frame_blue_starter";

            string color = "red";
            switch (cardType)
            {
                case CardType.Diet:     color = "red"; break;
                case CardType.Exercise: color = "green"; break;
                case CardType.Medicine: color = "purple"; break;
            }
            return $"Sprites/Cards/frames/frame_{color}_{cardRarity.ToString().ToLower()}";
        }


        private string GetCardTypeChinese(CardType type)
        {
            switch (type)
            {
                case CardType.Starter:  return "初始卡";
                case CardType.Diet:     return "膳食卡";
                case CardType.Exercise: return "运动卡";
                case CardType.Medicine: return "药物卡";
                default:                return "未知卡";
            }
        }

        private string GetCardRarityChinese(CardRarity rarity)
        {
            switch (rarity)
            {
                case CardRarity.Common:   return "普通";
                case CardRarity.Uncommon: return "良好";
                case CardRarity.Rare:     return "优秀";
                default:                  return "普通";
            }
        }
    }
}
