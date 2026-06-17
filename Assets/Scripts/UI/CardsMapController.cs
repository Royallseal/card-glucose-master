using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CGM.Data;
using CGM.Core;

namespace CGM.UI
{
    public enum CardsMapMode
    {
        DrawPile,
        DiscardPile,
        PlayerDeck,
        CardLibrary
    }

    /// <summary>
    /// 卡牌展示地图/面板控制器，多处复用展示抽牌堆、弃牌堆、本人牌组以及卡牌图鉴。
    /// 支持返回上一级导航栈。
    /// </summary>
    public class CardsMapController : MonoBehaviour
    {
        [Header("UI 组件引用")]
        [SerializeField] private TextMeshProUGUI titleText;
        [SerializeField] private Button exitButton;
        [SerializeField] private Transform contentContainer;
        [SerializeField] private GameObject cardPrefab;

        // 导航历史与状态
        private readonly Stack<CardsMapMode> modeHistory = new Stack<CardsMapMode>();
        private GameObject sourcePanel; // 进入此面板前处于激活状态的源面板
        private BattleSessionController battleController;

        private void Awake()
        {
            // 自动解析未绑定在 Inspector 中的子组件引用
            if (titleText == null)
            {
                Transform titleTextTrans = transform.Find("TitleUI/TitleText");
                if (titleTextTrans != null)
                {
                    titleText = titleTextTrans.GetComponent<TextMeshProUGUI>();
                }
            }

            if (exitButton == null)
            {
                Transform exitTrans = transform.Find("ExitUI");
                if (exitTrans != null)
                {
                    exitButton = exitTrans.GetComponent<Button>();
                }
            }

            if (contentContainer == null)
            {
                Transform contentTrans = transform.Find("Scroll View/Viewport/Content");
                if (contentTrans != null)
                {
                    contentContainer = contentTrans;
                }
            }

            if (exitButton != null)
            {
                exitButton.onClick.AddListener(CloseOrBack);
            }

            battleController = FindObjectOfType<BattleSessionController>();

            // 如果没有在 Inspector 中拖拽预制体，尝试从 Resources 动态加载
            if (cardPrefab == null)
            {
                cardPrefab = Resources.Load<GameObject>("Prefabs/Card");
            }
        }

        /// <summary>
        /// 打开卡牌展示面板。
        /// </summary>
        /// <param name="mode">展示的模式类型</param>
        /// <param name="srcPanel">源面板（退出此界面后恢复显示的面板）</param>
        public void Open(CardsMapMode mode, GameObject srcPanel)
        {
            // 如果第一次打开，记录来源并隐藏旧面板
            if (!gameObject.activeSelf)
            {
                sourcePanel = srcPanel;
                modeHistory.Clear();
                gameObject.SetActive(true);
                if (srcPanel != null)
                {
                    srcPanel.SetActive(false);
                }
            }

            // 防止在同一个展示类型下重复进入导致冲突
            if (modeHistory.Count > 0 && modeHistory.Peek() == mode)
            {
                return;
            }

            modeHistory.Push(mode);
            RefreshView();
        }

        /// <summary>
        /// 返回上一级或关闭界面。
        /// </summary>
        public void CloseOrBack()
        {
            if (modeHistory.Count > 1)
            {
                modeHistory.Pop();
                RefreshView();
            }
            else
            {
                // 关闭整个面板，恢复源面板
                gameObject.SetActive(false);
                if (sourcePanel != null)
                {
                    sourcePanel.SetActive(true);
                }
                modeHistory.Clear();
            }
        }

        /// <summary>
        /// 刷新当前的卡牌显示列表
        /// </summary>
        private void RefreshView()
        {
            if (modeHistory.Count == 0) return;

            CardsMapMode currentMode = modeHistory.Peek();

            // 1. 更新标题文字
            UpdateTitle(currentMode);

            // 2. 清理 Content 容器中的旧卡牌
            ClearContent();

            // 3. 获取对应的卡牌列表并排序
            List<CardInfo> cardsToDisplay = FetchAndSortCards(currentMode);

            // 4. 实例化并配置卡牌
            PopulateCards(cardsToDisplay);
        }

        private void UpdateTitle(CardsMapMode mode)
        {
            if (titleText == null) return;

            switch (mode)
            {
                case CardsMapMode.DrawPile:
                    titleText.text = "抽牌堆";
                    break;
                case CardsMapMode.DiscardPile:
                    titleText.text = "弃牌堆";
                    break;
                case CardsMapMode.PlayerDeck:
                    titleText.text = "我的牌组";
                    break;
                case CardsMapMode.CardLibrary:
                    titleText.text = "卡牌图鉴";
                    break;
            }
        }

        private void ClearContent()
        {
            if (contentContainer == null) return;

            foreach (Transform child in contentContainer)
            {
                Destroy(child.gameObject);
            }
        }

        private List<CardInfo> FetchAndSortCards(CardsMapMode mode)
        {
            List<CardInfo> list = new List<CardInfo>();

            if (battleController == null)
            {
                battleController = FindObjectOfType<BattleSessionController>();
            }

            switch (mode)
            {
                case CardsMapMode.DrawPile:
                    if (battleController != null && battleController.DrawPile != null)
                    {
                        // 复制一份，防止展示时透露下一回合抽牌的真实物理顺序，排序后更美观且安全
                        list.AddRange(battleController.DrawPile);
                    }
                    break;

                case CardsMapMode.DiscardPile:
                    if (battleController != null && battleController.DiscardPile != null)
                    {
                        list.AddRange(battleController.DiscardPile);
                    }
                    break;

                case CardsMapMode.PlayerDeck:
                    if (battleController != null && battleController.StartingDeckCardIds != null && CardDatabase.Instance != null)
                    {
                        foreach (string id in battleController.StartingDeckCardIds)
                        {
                            CardInfo info = CardDatabase.Instance.GetCardById(id);
                            if (info != null)
                            {
                                list.Add(info);
                            }
                        }
                    }
                    break;

                case CardsMapMode.CardLibrary:
                    if (CardDatabase.Instance != null)
                    {
                        // 卡牌图鉴：每种卡牌（按 id）只展示一张
                        var allCards = CardDatabase.Instance.GetAllCards();
                        var seenIds = new System.Collections.Generic.HashSet<string>();
                        foreach (var card in allCards)
                        {
                            if (card != null && seenIds.Add(card.id))
                            {
                                list.Add(card);
                            }
                        }
                    }
                    break;
            }

            // 按卡牌类型（Starter -> Diet -> Exercise -> Medicine）与 卡牌ID 排序，使其更加整齐美观
            list.Sort((a, b) =>
            {
                int typeA = GetTypePriority(a.type);
                int typeB = GetTypePriority(b.type);
                if (typeA != typeB)
                {
                    return typeA.CompareTo(typeB);
                }
                return string.Compare(a.id, b.id, StringComparison.Ordinal);
            });

            return list;
        }

        private int GetTypePriority(string typeStr)
        {
            switch (typeStr)
            {
                case "Starter": return 0;
                case "Diet": return 1;
                case "Exercise": return 2;
                case "Medicine": return 3;
                default: return 4;
            }
        }

        private void PopulateCards(List<CardInfo> cards)
        {
            if (contentContainer == null || cardPrefab == null) return;

            foreach (var card in cards)
            {
                GameObject cardGo = Instantiate(cardPrefab, contentContainer);
                cardGo.name = $"DisplayCard_{card.id}";

                // 渲染卡面数据
                CardUI cardUI = cardGo.GetComponent<CardUI>();
                if (cardUI != null)
                {
                    cardUI.SetCard(card);
                }

                // 启用 CardDragHandler 但设置为仅展示，保留 hover 和音效
                CardDragHandler dragHandler = cardGo.GetComponent<CardDragHandler>();
                if (dragHandler == null)
                {
                    dragHandler = cardGo.AddComponent<CardDragHandler>();
                }
                dragHandler.SetDisplayOnly(true);
                dragHandler.SetCardInfo(card);

                Button cardBtn = cardGo.GetComponent<Button>();
                if (cardBtn != null)
                {
                    cardBtn.onClick.RemoveAllListeners();
                    // 不将 interactable 设为 false 以免影响 Raycast
                }
            }
        }
    }
}
