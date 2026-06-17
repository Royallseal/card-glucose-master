using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CGM.Data;
using CGM.Core;

namespace CGM.UI
{
    /// <summary>
    /// 商店面板总控，负责每次进入商店时生成 1 普通 + 1 良好 + 1 优质卡牌，并维护购买及金币判定流程。
    /// </summary>
    public class ShopController : MonoBehaviour
    {
        [Header("音效配置")]
        [SerializeField] private AudioClip hoverAudioClip;
        [SerializeField] private AudioClip clickAudioClip;

        private PlayerStats playerStats;
        private ShopCardInteraction selectedCard;
        private readonly List<ShopCardInteraction> shopCards = new List<ShopCardInteraction>();

        private void Awake()
        {
            // 不在此处 FindObjectOfType，因为 ShopPanel 激活时 BattlePanel 可能已隐藏，
            // PlayerStats 所在物体 inactive 导致查找失败。由 GameSessionManager 通过 SetPlayerStats 注入。
        }

        /// <summary>
        /// 由 GameSessionManager 注入 PlayerStats 引用，避免依赖 FindObjectOfType 查找 inactive 对象。
        /// </summary>
        public void SetPlayerStats(PlayerStats stats)
        {
            playerStats = stats;
        }

        private void OnEnable()
        {
            InitializeShop();
        }

        /// <summary>
        /// 初始化商店商品列表与价格
        /// </summary>
        public void InitializeShop()
        {
            if (playerStats == null)
            {
                playerStats = FindObjectOfType<PlayerStats>();
            }

            selectedCard = null;
            shopCards.Clear();

            // 1. 获取 Content 容器（ShopPanel → CardListPanel → Cards → Scroll View → Viewport → Content）
            Transform contentTrans = transform.Find("CardListPanel/Cards/Scroll View/Viewport/Content");
            if (contentTrans == null)
            {
                // 备用路径：递归查找最内层 Content
                contentTrans = FindDeepChild(transform, "Content");
            }
            if (contentTrans == null)
            {
                Debug.LogError("[ShopController] 未能找到 Content 节点。");
                return;
            }

            // 清除 Content 中的旧卡牌（用 DestroyImmediate 确保立即移除，避免与后续 Find 冲突）
            for (int i = contentTrans.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(contentTrans.GetChild(i).gameObject);
            }

            // 确保 CardDatabase 加载就绪
            if (CardDatabase.Instance == null)
            {
                Debug.LogError("[ShopController] CardDatabase 实例为空，无法加载商品。");
                return;
            }

            // 2. 分别获取普通、良好、优质的全部卡牌候选池（剔除 Starter 初始卡）
            List<CardInfo> commonPool = GetCardsOfRarityFiltered(CardRarity.Common);
            List<CardInfo> uncommonPool = GetCardsOfRarityFiltered(CardRarity.Uncommon);
            List<CardInfo> rarePool = GetCardsOfRarityFiltered(CardRarity.Rare);

            if (commonPool.Count == 0 || uncommonPool.Count == 0 || rarePool.Count == 0)
            {
                Debug.LogError($"[ShopController] 卡牌候选池为空 Common={commonPool.Count} Uncommon={uncommonPool.Count} Rare={rarePool.Count}");
                return;
            }

            // 3. 随机抽取商品
            CardInfo commonCard = commonPool[Random.Range(0, commonPool.Count)];
            CardInfo uncommonCard = uncommonPool[Random.Range(0, uncommonPool.Count)];
            CardInfo rareCard = rarePool[Random.Range(0, rarePool.Count)];

            // 4. 生成价格（普通 80~100，良好 115~145，优质 200~240）
            int commonPrice = Random.Range(80, 101);
            int uncommonPrice = Random.Range(115, 146);
            int rarePrice = Random.Range(200, 241);

            Debug.Log($"[ShopController] 生成商品: {commonCard.name}({commonPrice}G) | {uncommonCard.name}({uncommonPrice}G) | {rareCard.name}({rarePrice}G)  玩家金币={playerStats?.Gold ?? -1}");

            // 5. 对 Content 下的 3 张预置卡牌进行装载
            ConfigureCardSlot(contentTrans, 0, "Card", commonCard, commonPrice);
            ConfigureCardSlot(contentTrans, 1, "Card (1)", uncommonCard, uncommonPrice);
            ConfigureCardSlot(contentTrans, 2, "Card (2)", rareCard, rarePrice);

            // 6. 全局刷新一次可购买状态与置灰效果
            UpdateCardAffordability();
            Debug.Log($"[ShopController] 商店初始化完成，Content 子物体数={contentTrans.childCount}");
        }

        private List<CardInfo> GetCardsOfRarityFiltered(CardRarity rarity)
        {
            List<CardInfo> all = CardDatabase.Instance.GetCardsByRarity(rarity);
            List<CardInfo> filtered = new List<CardInfo>();
            foreach (var c in all)
            {
                if (c.GetCardType() != CardType.Starter)
                {
                    filtered.Add(c);
                }
            }
            return filtered;
        }

        private void ConfigureCardSlot(Transform content, int index, string childName, CardInfo card, int price)
        {
            Transform slotTrans = content.Find(childName);
            if (slotTrans == null && content.childCount > index)
            {
                slotTrans = content.GetChild(index);
            }

            // 如果 Content 中没有预置卡牌槽位，则从预制体动态生成
            GameObject cardGo;
            if (slotTrans == null)
            {
                GameObject prefab = Resources.Load<GameObject>("Prefabs/Card");
                if (prefab != null)
                {
                    cardGo = Instantiate(prefab, content);
                    cardGo.name = childName;
                }
                else
                {
                    Debug.LogError("[ShopController] 无法加载卡牌预制体 Resources/Prefabs/Card");
                    return;
                }
            }
            else
            {
                cardGo = slotTrans.gameObject;
            }

            cardGo.SetActive(true);
            cardGo.transform.localScale = Vector3.one;

            // 渲染基本卡面
            CardUI cardUI = cardGo.GetComponent<CardUI>();
            if (cardUI != null)
            {
                cardUI.SetCard(card);
            }

            // 确保金币价格 UI 容器显示
            Transform goldValueTrans = cardGo.transform.Find("GoldValue");
            if (goldValueTrans != null)
            {
                goldValueTrans.gameObject.SetActive(true);
            }

            // 显示价格文本
            Transform priceValTrans = cardGo.transform.Find("GoldValue/Gold/Gold_Value");
            if (priceValTrans != null)
            {
                var txt = priceValTrans.GetComponent<TextMeshProUGUI>();
                if (txt != null)
                {
                    txt.text = price.ToString();
                }
            }

            // 禁用战斗拖拽脚本
            CardDragHandler drag = cardGo.GetComponent<CardDragHandler>();
            if (drag != null) drag.enabled = false;

            // 挂载/获取商店交互脚本
            ShopCardInteraction interaction = cardGo.GetComponent<ShopCardInteraction>();
            if (interaction == null)
            {
                interaction = cardGo.AddComponent<ShopCardInteraction>();
            }

            interaction.Initialize(card, price, OnCardClicked, hoverAudioClip, clickAudioClip);
            shopCards.Add(interaction);
        }

        private void OnCardClicked(ShopCardInteraction clickedCard)
        {
            if (clickedCard == null) return;

            if (selectedCard == null)
            {
                // 第一次点击：选中该卡牌
                selectedCard = clickedCard;
                selectedCard.SetSelected(true);
            }
            else if (selectedCard == clickedCard)
            {
                // 第二次点击：执行购买
                PurchaseCard(clickedCard);
            }
            else
            {
                // 点击了另一张卡牌：切换选中状态
                selectedCard.SetSelected(false);
                selectedCard = clickedCard;
                selectedCard.SetSelected(true);
            }
        }

        private void PurchaseCard(ShopCardInteraction card)
        {
            if (playerStats == null || playerStats.Gold < card.Price) return;

            // 扣除金币
            playerStats.ChangeGold(-card.Price);

            // 清理当前选中状态
            selectedCard = null;
            shopCards.Remove(card);

            // 启动飞入卡组动画
            StartCoroutine(AnimateCardFlyToDeck(card.gameObject, card.CardInfo.id));

            // 每次购买后都要更新其余卡牌的可购买性（金币减少了）
            UpdateCardAffordability();
        }

        /// <summary>
        /// 全局检测并更新卡牌的“买得起/买不起”视觉效果与点击权限
        /// </summary>
        public void UpdateCardAffordability()
        {
            int currentGold = playerStats != null ? playerStats.Gold : 0;

            foreach (var card in shopCards)
            {
                if (card != null && card.gameObject.activeSelf)
                {
                    card.SetAffordable(currentGold >= card.Price);
                }
            }
        }

        private IEnumerator AnimateCardFlyToDeck(GameObject cardGo, string cardId)
        {
            // 寻找 UItop 卡组的目标位置
            Transform deckTarget = null;
            var ultop = FindObjectOfType<UI.UltopController>();
            if (ultop != null)
            {
                deckTarget = ultop.transform.Find("Icon_Line/Cards");
            }

            if (deckTarget != null)
            {
                Canvas canvas = FindObjectOfType<Canvas>();
                GameObject flyClone = Instantiate(cardGo, canvas.transform);
                flyClone.name = "FlyCardClone_" + cardId;

                // 去除交互事件
                Destroy(flyClone.GetComponent<ShopCardInteraction>());
                var cg = flyClone.GetComponent<CanvasGroup>();
                if (cg == null) cg = flyClone.AddComponent<CanvasGroup>();
                cg.blocksRaycasts = false;

                // 飞入时隐藏价格框
                Transform goldValueSub = flyClone.transform.Find("GoldValue");
                if (goldValueSub != null) goldValueSub.gameObject.SetActive(false);

                flyClone.transform.position = cardGo.transform.position;
                flyClone.transform.localScale = cardGo.transform.localScale;

                // 隐藏原商品
                cardGo.SetActive(false);

                Vector3 startPos = flyClone.transform.position;
                Vector3 startScale = flyClone.transform.localScale;
                float elapsed = 0f;
                float duration = 0.75f;

                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / duration;
                    float easedT = t * (2 - t);

                    flyClone.transform.position = Vector3.Lerp(startPos, deckTarget.position, easedT);
                    flyClone.transform.localScale = Vector3.Lerp(startScale, Vector3.one * 0.15f, easedT);
                    cg.alpha = Mathf.Lerp(1.0f, 0f, easedT);

                    yield return null;
                }

                Destroy(flyClone);
            }
            else
            {
                cardGo.SetActive(false);
            }

            // 将卡牌加入玩家持久化起始卡组
            var battleController = FindObjectOfType<BattleSessionController>();
            if (battleController != null)
            {
                battleController.AddCardToStartingDeck(cardId);
            }

            // 刷新顶部 UI 卡牌数
            if (ultop != null)
            {
                ultop.UpdateCardsCount();
            }
        }

        /// <summary>
        /// 递归深度优先查找指定名称的子 Transform（用于路径不确定时的兜底查找）。
        /// </summary>
        private Transform FindDeepChild(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name) return child;
                Transform found = FindDeepChild(child, name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
