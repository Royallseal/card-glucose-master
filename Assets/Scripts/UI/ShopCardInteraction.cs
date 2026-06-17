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
            hoverSound = hoverClip;
            clickSound = clickClip;

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
                // 金币不够时价格显示为红色，足够时显示为黄色
                priceText.color = affordable 
                    ? TryParseColor("#FFAD1F", Color.yellow) 
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
            if (!isAffordable) return;

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
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!isAffordable) return;

            isHovered = false;
            if (!isSelected)
            {
                targetScale = 1.0f;
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
    }
}
