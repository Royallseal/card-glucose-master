using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using CGM.Data;
using CGM.Core;

namespace CGM.UI
{
    /// <summary>
    /// 结算卡牌奖励特有交互脚本，处理 Hover 放大、选中固定放大与排他性。
    /// </summary>
    public class RewardCardInteraction : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public CardInfo CardInfo { get; private set; }
        
        private bool _isSelected = false;
        private Vector3 _originalScale;
        private Coroutine _scaleCoroutine;
        private Action<RewardCardInteraction> _onClickCallback;

        private const float HoverScale = 1.12f;
        private const float SelectedScale = 1.15f;
        private const float LerpDuration = 0.12f;

        private Canvas _canvasComponent;

        public void Initialize(CardInfo card, Action<RewardCardInteraction> onClick)
        {
            CardInfo = card;
            _onClickCallback = onClick;
            _isSelected = false;
            _originalScale = Vector3.one;
            transform.localScale = _originalScale;

            // 动态挂载 Sub-Canvas 确保层级置顶，表现同手卡 Hover 置顶效果一致
            _canvasComponent = GetComponent<Canvas>();
            if (_canvasComponent == null)
            {
                _canvasComponent = gameObject.AddComponent<Canvas>();
            }
            _canvasComponent.overrideSorting = false;

            if (GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
            {
                gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_isSelected) return;
            StartScaleLerp(_originalScale * HoverScale);
            
            if (_canvasComponent != null)
            {
                _canvasComponent.overrideSorting = true;
                _canvasComponent.sortingOrder = 35;
            }

            // 播放卡牌 Hover 音效
            AudioClip cardHoverSound = Resources.Load<AudioClip>("Audio/Card_Hover");
            if (cardHoverSound != null)
            {
                Vector3 pos = Camera.main != null ? Camera.main.transform.position : transform.position;
                AudioManager.PlaySfxStatic(cardHoverSound, pos);
            }

            TryShowTooltip();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_isSelected) return;
            StartScaleLerp(_originalScale);

            if (_canvasComponent != null)
            {
                _canvasComponent.overrideSorting = false;
            }

            if (TooltipManager.Instance != null)
            {
                TooltipManager.Instance.HideTooltip(transform as RectTransform);
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_isSelected) return;

            if (TooltipManager.Instance != null)
            {
                TooltipManager.Instance.HideTooltip(transform as RectTransform);
            }

            _onClickCallback?.Invoke(this);
        }

        public void SetSelected(bool selected)
        {
            _isSelected = selected;
            if (_isSelected)
            {
                StartScaleLerp(_originalScale * SelectedScale);
                if (_canvasComponent != null)
                {
                    _canvasComponent.overrideSorting = true;
                    _canvasComponent.sortingOrder = 40; // 选中的卡片拥有最高绘制层级，盖在其它卡片上方
                }
            }
            else
            {
                StartScaleLerp(_originalScale);
                if (_canvasComponent != null)
                {
                    _canvasComponent.overrideSorting = false;
                }
            }
        }

        private void StartScaleLerp(Vector3 target)
        {
            if (_scaleCoroutine != null) StopCoroutine(_scaleCoroutine);
            _scaleCoroutine = StartCoroutine(ScaleRoutine(target));
        }

        private IEnumerator ScaleRoutine(Vector3 target)
        {
            Vector3 start = transform.localScale;
            float elapsed = 0f;
            while (elapsed < LerpDuration)
            {
                elapsed += Time.deltaTime;
                transform.localScale = Vector3.Lerp(start, target, elapsed / LerpDuration);
                yield return null;
            }
            transform.localScale = target;
            _scaleCoroutine = null;
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
