using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using CGM.Data;

namespace CGM.UI
{
    /// <summary>
    /// 结算卡牌奖励特有交互脚本，处理 Hover 放大、选中固定放大与排他性。
    /// </summary>
    public class RewardCardInteraction : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public CardInfo CardInfo { get; private set; }
        private bool isSelected = false;
        private Vector3 originalScale;
        private Coroutine scaleCoroutine;
        private Action<RewardCardInteraction> onClickCallback;

        private const float HoverScale = 1.12f;
        private const float SelectedScale = 1.15f;
        private const float LerpDuration = 0.12f;

        private Canvas canvasComponent;

        public void Initialize(CardInfo card, Action<RewardCardInteraction> onClick)
        {
            CardInfo = card;
            onClickCallback = onClick;
            isSelected = false;
            originalScale = Vector3.one;
            transform.localScale = originalScale;

            // 动态挂载 Sub-Canvas 确保层级置顶，表现同手卡 Hover 置顶效果一致
            canvasComponent = GetComponent<Canvas>();
            if (canvasComponent == null)
            {
                canvasComponent = gameObject.AddComponent<Canvas>();
            }
            canvasComponent.overrideSorting = false;

            if (GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
            {
                gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (isSelected) return;
            StartScaleLerp(originalScale * HoverScale);
            
            if (canvasComponent != null)
            {
                canvasComponent.overrideSorting = true;
                canvasComponent.sortingOrder = 35;
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (isSelected) return;
            StartScaleLerp(originalScale);

            if (canvasComponent != null)
            {
                canvasComponent.overrideSorting = false;
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (isSelected) return;
            onClickCallback?.Invoke(this);
        }

        public void SetSelected(bool selected)
        {
            isSelected = selected;
            if (isSelected)
            {
                StartScaleLerp(originalScale * SelectedScale);
                if (canvasComponent != null)
                {
                    canvasComponent.overrideSorting = true;
                    canvasComponent.sortingOrder = 40; // 选中的卡片拥有最高绘制层级，盖在其它卡片上方
                }
            }
            else
            {
                StartScaleLerp(originalScale);
                if (canvasComponent != null)
                {
                    canvasComponent.overrideSorting = false;
                }
            }
        }

        private void StartScaleLerp(Vector3 target)
        {
            if (scaleCoroutine != null) StopCoroutine(scaleCoroutine);
            scaleCoroutine = StartCoroutine(ScaleRoutine(target));
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
            scaleCoroutine = null;
        }
    }
}
