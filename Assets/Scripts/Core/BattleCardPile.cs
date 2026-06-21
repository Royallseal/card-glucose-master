using System.Collections.Generic;
using UnityEngine;
using CGM.Data;

namespace CGM.Core
{
    /// <summary>
    /// 战斗中的牌堆、手牌与弃牌堆管理器。
    /// </summary>
    public sealed class BattleCardPile
    {
        private readonly List<CardInfo> drawPile = new List<CardInfo>();
        private readonly List<CardInfo> handPile = new List<CardInfo>();
        private readonly List<CardInfo> discardPile = new List<CardInfo>();

        public IReadOnlyList<CardInfo> DrawPile => drawPile;
        public IReadOnlyList<CardInfo> Hand => handPile;
        public IReadOnlyList<CardInfo> DiscardPile => discardPile;

        public int DrawCount => drawPile.Count;
        public int HandCount => handPile.Count;
        public int DiscardCount => discardPile.Count;

        /// <summary>
        /// 用新的起始牌组重置三类区域。
        /// </summary>
        public void Reset(IEnumerable<CardInfo> startingDeck)
        {
            drawPile.Clear();
            handPile.Clear();
            discardPile.Clear();

            if (startingDeck == null)
            {
                return;
            }

            foreach (var card in startingDeck)
            {
                if (card != null)
                {
                    drawPile.Add(card);
                }
            }

            RandomManager.Shuffle(drawPile);
        }

        /// <summary>
        /// 向抽牌堆底部或顶部添加卡牌（供调试及未来特殊机制使用）。
        /// </summary>
        public void AddToDrawPile(CardInfo card)
        {
            if (card != null)
            {
                drawPile.Add(card);
            }
        }

        /// <summary>
        /// 直接向弃牌堆添加卡牌（供调试与时序结算使用）。
        /// </summary>
        public void AddToDiscardPile(CardInfo card)
        {
            if (card != null)
            {
                discardPile.Add(card);
            }
        }

        /// <summary>
        /// 从抽牌堆中摸牌，直到达到数量上限或手牌上限。
        /// </summary>
        public List<CardInfo> Draw(int count, int maxHandSize, System.Action<CardInfo> onOverflow = null)
        {
            List<CardInfo> drawnCards = new List<CardInfo>();

            if (count <= 0)
            {
                return drawnCards;
            }

            for (int i = 0; i < count; i++)
            {
                if (drawPile.Count == 0)
                {
                    RefillDrawPileFromDiscardPile();
                }

                if (drawPile.Count == 0)
                {
                    break;
                }

                int lastIndex = drawPile.Count - 1;
                CardInfo card = drawPile[lastIndex];
                drawPile.RemoveAt(lastIndex);

                if (handPile.Count >= maxHandSize)
                {
                    discardPile.Add(card);
                    onOverflow?.Invoke(card);
                }
                else
                {
                    handPile.Add(card);
                    drawnCards.Add(card);
                }
            }

            return drawnCards;
        }

        /// <summary>
        /// 判断手牌中是否存在指定卡牌。
        /// </summary>
        public bool ContainsInHand(CardInfo card)
        {
            if (card == null) return false;
            foreach (var c in handPile)
            {
                if (c != null && c.id == card.id) return true;
            }
            return false;
        }

        /// <summary>
        /// 从手牌中移除一张卡。
        /// </summary>
        public bool RemoveFromHand(CardInfo card)
        {
            if (card == null)
            {
                return false;
            }

            for (int i = 0; i < handPile.Count; i++)
            {
                if (handPile[i] != null && handPile[i].id == card.id)
                {
                    handPile.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 将一张卡从手牌送入弃牌堆。
        /// </summary>
        public bool DiscardCard(CardInfo card)
        {
            if (!RemoveFromHand(card))
            {
                return false;
            }

            discardPile.Add(card);
            return true;
        }

        /// <summary>
        /// 将整手牌送入弃牌堆。
        /// </summary>
        public void DiscardHand()
        {
            if (handPile.Count == 0)
            {
                return;
            }

            discardPile.AddRange(handPile);
            handPile.Clear();
        }

        /// <summary>
        /// 当抽牌堆空时，把弃牌堆洗回抽牌堆。
        /// </summary>
        public void RefillDrawPileFromDiscardPile()
        {
            if (discardPile.Count == 0)
            {
                return;
            }

            drawPile.AddRange(discardPile);
            discardPile.Clear();
            RandomManager.Shuffle(drawPile);
        }
    }
}