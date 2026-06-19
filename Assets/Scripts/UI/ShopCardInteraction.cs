using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using CGM.Data;

namespace CGM.UI
{
    /// <summary>
    /// 商店卡牌特殊交互组件，处理 Hover 缩放与音效、点击单选、二次点击购买及余额不足置灰禁用。
    /// </summary>
    public class ShopCardInteraction : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public CardInfo CardInfo { get; private set; }
        public int Price { get; private set; }

        private bool isSelected = false;
        private bool isHovered = false;
        private bool isAffordable = true;

        private float targetScale = 1.0f;
        private float currentScale = 1.0f;
        private const float LerpSpeed = 12f;

        private Action<ShopCardInteraction> onClickCallback;
        private AudioClip hoverSound;
        private AudioClip clickSound;
        private CanvasGroup canvasGroup;
        private TextMeshProUGUI priceText;

        public void Initialize(CardInfo card, int price, Action<ShopCardInteraction> clickCallback, AudioClip hoverClip, AudioClip clickClip)
        {
            CardInfo = card;
            Price = price;
            onClickCallback = clickCallback;
            clickSound = clickClip;

            // 统一配置卡牌 Hover 音效为 Card_Hover
            hoverSound = Resources.Load<AudioClip>("Audio/Card_Hover");
            if (hoverSound == null)
            {
                hoverSound = hoverClip;
            }

            // 确保卡牌有独立的 Canvas + GraphicRaycaster，供 ShopCardInteraction 独占处理所有指针事件。
            // 同时移除可能冲突的 CardDragHandler，避免双重 hover/click 响应。
            var cardCanvas = GetComponent<Canvas>();
            if (cardCanvas == null)
            {
                cardCanvas = gameObject.AddComponent<Canvas>();
                cardCanvas.overrideSorting = false;
            }
            if (GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
            {
                gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            }

            // 移除冲突的 CardDragHandler（如果有），由 ShopCardInteraction 独占指针事件
            var dragHandler = GetComponent<CardDragHandler>();
            if (dragHandler != null)
            {
                Destroy(dragHandler);
            }

            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            // 寻找价格文字组件
            Transform priceTextTrans = transform.Find("GoldValue/Gold/Gold_Value");
            if (priceTextTrans != null)
            {
                priceText = priceTextTrans.GetComponent<TextMeshProUGUI>();
            }

            // 重置状态
            isSelected = false;
            isHovered = false;
            targetScale = 1.0f;
            currentScale = 1.0f;
            transform.localScale = Vector3.one;
        }

        public void SetAffordable(bool affordable)
        {
            isAffordable = affordable;

            if (canvasGroup != null)
            {
                // 金币不够时，卡牌半透明且无法阻挡射线（禁止一切交互）
                canvasGroup.alpha = affordable ? 1.0f : 0.6f;
                canvasGroup.blocksRaycasts = affordable;
            }

            if (priceText != null)
            {
                // 买得起显示绿色，买不起显示红色
                priceText.color = affordable 
                    ? TryParseColor("#4EC9B0", Color.green) 
                    : TryParseColor("#FF6B6B", Color.red);
            }

            // 如果原本处于选中状态但突然买不起了，立即退选
            if (!affordable && isSelected)
            {
                SetSelected(false);
            }
        }

        public void SetSelected(bool selected)
        {
            isSelected = selected;
            targetScale = selected ? 1.12f : (isHovered ? 1.06f : 1.0f);
        }

        private void Update()
        {
            // 平滑缩放插值，金币框会作为子物体随之一同缩放
            if (Mathf.Abs(currentScale - targetScale) > 0.001f)
            {
                currentScale = Mathf.Lerp(currentScale, targetScale, Time.deltaTime * LerpSpeed);
                transform.localScale = Vector3.one * currentScale;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            Debug.Log($"[ShopCardInteraction] OnPointerEnter on card: {(CardInfo != null ? CardInfo.name : "null")}");
            if (!isAffordable)
            {
                Debug.Log("[ShopCardInteraction] Hover ignored: not affordable.");
                return;
            }

            isHovered = true;
            if (!isSelected)
            {
                targetScale = 1.06f;
            }

            // 播放悬停音效
            if (hoverSound != null && Camera.main != null)
            {
                AudioSource.PlayClipAtPoint(hoverSound, Camera.main.transform.position);
            }

            TryShowTooltip();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            Debug.Log($"[ShopCardInteraction] OnPointerExit on card: {(CardInfo != null ? CardInfo.name : "null")}");
            if (!isAffordable) return;

            isHovered = false;
            if (!isSelected)
            {
                targetScale = 1.0f;
            }

            if (TooltipManager.Instance != null)
            {
                TooltipManager.Instance.HideTooltip();
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!isAffordable) return;

            // 播放点击/选中音效
            if (clickSound != null && Camera.main != null)
            {
                AudioSource.PlayClipAtPoint(clickSound, Camera.main.transform.position);
            }

            if (TooltipManager.Instance != null)
            {
                TooltipManager.Instance.HideTooltip();
            }

            // 回调通知总控
            onClickCallback?.Invoke(this);
        }

        private Color TryParseColor(string hex, Color defaultColor)
        {
            if (ColorUtility.TryParseHtmlString(hex, out Color color))
            {
                return color;
            }
            return defaultColor;
        }

        private void TryShowTooltip()
        {
            Debug.Log($"[ShopCardInteraction] TryShowTooltip for card: {(CardInfo != null ? CardInfo.name : "null")}");
            if (TooltipManager.Instance == null)
            {
                Debug.LogWarning("[ShopCardInteraction] TooltipManager.Instance is null!");
                return;
            }
            if (CardInfo == null)
            {
                Debug.LogWarning("[ShopCardInteraction] CardInfo is null!");
                return;
            }
            if (CardInfo.effects == null)
            {
                Debug.LogWarning("[ShopCardInteraction] CardInfo.effects is null!");
                return;
            }

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            int count = 0;

            foreach (var effect in CardInfo.effects)
            {
                if (effect.effectType == "apply_buff" || effect.effectType == "apply_debuff")
                {
                    try
                    {
                        BuffId buffId = effect.GetBuffId();
                        var buffInfo = BuffDatabase.Get(buffId);
                        if (buffInfo != null)
                        {
                            if (count > 0) sb.Append("\n\n");
                            sb.Append($"<color={buffInfo.colorHex}><b>{buffInfo.name}</b></color>\n{buffInfo.description}");
                            count++;
                        }
                        else
                        {
                            Debug.LogWarning($"[ShopCardInteraction] BuffDatabase has no entry for buffId: {buffId}");
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"[ShopCardInteraction] Exception parsing effect buff: {ex.Message}");
                    }
                }
            }

            Debug.Log($"[ShopCardInteraction] Buff/Debuff effect count: {count}");
            if (count > 0)
            {
                TooltipManager.Instance.ShowTooltip(sb.ToString(), transform as RectTransform);
            }
            else
            {
                Debug.Log("[ShopCardInteraction] Card has no apply_buff/debuff effects. No tooltip will be shown.");
            }
        }
    }
}
