using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using CGM.Data;
using CGM.Core;

namespace CGM.UI
{
    /// <summary>
    /// 商店卡牌特殊交互组件，处理 Hover 缩放与音效、点击单选、二次点击购买及余额不足置灰禁用。
    /// </summary>
    public class ShopCardInteraction : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public CardInfo CardInfo { get; private set; }
        public int Price { get; private set; }

        private bool _isSelected = false;
        private bool _isHovered = false;
        private bool _isAffordable = true;

        private float _targetScale = 1.0f;
        private float _currentScale = 1.0f;
        private const float LerpSpeed = 12f;

        private Action<ShopCardInteraction> _onClickCallback;
        private AudioClip _hoverSound;
        private AudioClip _clickSound;
        private CanvasGroup _canvasGroup;
        private TextMeshProUGUI _priceText;

        public void Initialize(CardInfo card, int price, Action<ShopCardInteraction> clickCallback, AudioClip hoverClip, AudioClip clickClip)
        {
            CardInfo = card;
            Price = price;
            _onClickCallback = clickCallback;
            _clickSound = clickClip;

            // 统一配置卡牌 Hover 音效为 Card_Hover
            _hoverSound = Resources.Load<AudioClip>("Audio/Card_Hover");
            if (_hoverSound == null)
            {
                _hoverSound = hoverClip;
            }

            // 确保卡牌有独立的 Canvas + GraphicRaycaster，供 ShopCardInteraction 独占处理所有指针事件。
            // 同时移除可能冲突 durable CardDragHandler，避免双重 hover/click 响应。
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

            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
            {
                _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            // 寻找价格文字组件
            Transform priceTextTrans = transform.Find("GoldValue/Gold/Gold_Value");
            if (priceTextTrans != null)
            {
                _priceText = priceTextTrans.GetComponent<TextMeshProUGUI>();
            }

            // 重置状态
            _isSelected = false;
            _isHovered = false;
            _targetScale = 1.0f;
            _currentScale = 1.0f;
            transform.localScale = Vector3.one;
        }

        public void SetAffordable(bool affordable)
        {
            _isAffordable = affordable;

            if (_canvasGroup != null)
            {
                // 金币不够时，卡牌半透明且无法阻挡射线（禁止一切交互）
                _canvasGroup.alpha = affordable ? 1.0f : 0.6f;
                _canvasGroup.blocksRaycasts = affordable;
            }

            if (_priceText != null)
            {
                // 买得起显示绿色，买不起显示红色
                _priceText.color = affordable 
                    ? TryParseColor("#4EC9B0", Color.green) 
                    : TryParseColor("#FF6B6B", Color.red);
            }

            // 如果原本处于选中状态但突然买不起了，立即退选
            if (!affordable && _isSelected)
            {
                SetSelected(false);
            }
        }

        public void SetSelected(bool selected)
        {
            _isSelected = selected;
            _targetScale = selected ? 1.12f : (_isHovered ? 1.06f : 1.0f);
        }

        private void Update()
        {
            // 平滑缩放插值，金币框会作为子物体随之一同缩放
            if (Mathf.Abs(_currentScale - _targetScale) > 0.001f)
            {
                _currentScale = Mathf.Lerp(_currentScale, _targetScale, Time.deltaTime * LerpSpeed);
                transform.localScale = Vector3.one * _currentScale;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!_isAffordable) return;

            _isHovered = true;
            if (!_isSelected)
            {
                _targetScale = 1.06f;
            }

            // 播放悬停音效
            if (_hoverSound != null && Camera.main != null)
            {
                AudioManager.PlaySfxStatic(_hoverSound, Camera.main.transform.position);
            }

            TryShowTooltip();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!_isAffordable) return;

            _isHovered = false;
            if (!_isSelected)
            {
                _targetScale = 1.0f;
            }

            if (TooltipManager.Instance != null)
            {
                TooltipManager.Instance.HideTooltip();
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!_isAffordable) return;

            // 播放点击/选中音效
            if (_clickSound != null && Camera.main != null)
            {
                AudioManager.PlaySfxStatic(_clickSound, Camera.main.transform.position);
            }

            if (TooltipManager.Instance != null)
            {
                TooltipManager.Instance.HideTooltip();
            }

            // 回调通知总控
            _onClickCallback?.Invoke(this);
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
            if (TooltipManager.Instance != null && CardInfo != null)
            {
                TooltipManager.Instance.ShowCardEffectsTooltip(CardInfo, transform as RectTransform);
            }
        }
    }
}
