// =============================================================================
// CardAnimator.cs — 卡牌动效
// 挂载在卡牌预制体上。提供抽牌入场、弃牌飞出的协程动画。
// =============================================================================

using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace CGM.UI
{
    [RequireComponent(typeof(CanvasGroup), typeof(RectTransform))]
    public class CardAnimator : MonoBehaviour
    {
        [Header("抽牌动画")]
        [SerializeField] private float drawDuration = 0.25f;

        [Header("弃牌动画")]
        [SerializeField] private float discardDuration = 0.2f;
        [SerializeField] private float discardFadeDuration = 0.15f;

        private RectTransform _rect;
        private CanvasGroup _canvasGroup;
        private bool _animating;

        private void Awake()
        {
            _rect = GetComponent<RectTransform>();
            _canvasGroup = GetComponent<CanvasGroup>();
        }

        /// <summary>
        /// 抽牌入场：从 startPos 飞到 endPos。
        /// returnParent: 动画完成后移回的父节点（handContainer），null 则不移动。
        /// 调用前应已将 gameObject 移出 LayoutGroup 到 Canvas 根节点。
        /// </summary>
        public void PlayDrawAnimation(Vector2 worldStartPos, Vector2 worldEndPos, Transform returnParent = null, System.Action onComplete = null)
        {
            StartCoroutine(DrawRoutine(worldStartPos, worldEndPos, returnParent, onComplete));
        }

        private IEnumerator DrawRoutine(Vector2 from, Vector2 to, Transform returnParent, System.Action onComplete)
        {
            _animating = true;
            _canvasGroup.alpha = 0f;
            _rect.position = from;
            _rect.localScale = Vector3.one * 0.6f;

            float t = 0f;
            while (t < drawDuration)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / drawDuration);
                float eased = 1f - (1f - p) * (1f - p);
                _rect.position = Vector2.Lerp(from, to, eased);
                _canvasGroup.alpha = Mathf.Lerp(0f, 1f, eased);
                _rect.localScale = Vector3.Lerp(Vector3.one * 0.6f, Vector3.one, eased);
                yield return null;
            }

            _canvasGroup.alpha = 1f;
            _rect.localScale = Vector3.one;

            // 动画结束后移回 handContainer，让 LayoutGroup 接管位置
            if (returnParent != null)
            {
                transform.SetParent(returnParent, true);
                LayoutRebuilder.MarkLayoutForRebuild(returnParent.GetComponent<RectTransform>());
            }
            _animating = false;
            onComplete?.Invoke();
        }

        /// <summary>
        /// 弃牌飞出：向目标位置移动并淡出，完成后销毁。
        /// </summary>
        public void PlayDiscardAnimation(Vector2 worldTarget, System.Action onComplete = null)
        {
            StartCoroutine(DiscardRoutine(worldTarget, onComplete));
        }

        private IEnumerator DiscardRoutine(Vector2 worldTarget, System.Action onComplete)
        {
            _animating = true;
            var canvas = GetComponentInParent<Canvas>();
            Vector2 startPos = _rect.position;
            Vector2 endPos = worldTarget;

            float t = 0f;
            while (t < discardDuration)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / discardDuration);
                float eased = p * p; // ease-in quad
                _rect.position = Vector2.Lerp(startPos, endPos, eased);

                if (t > discardDuration - discardFadeDuration)
                    _canvasGroup.alpha = 1f - (t - (discardDuration - discardFadeDuration)) / discardFadeDuration;

                yield return null;
            }

            _canvasGroup.alpha = 0f;
            onComplete?.Invoke();
            Destroy(gameObject);
        }

        /// <summary>
        /// 立即销毁（无动画）。
        /// </summary>
        public void DestroyImmediate()
        {
            StopAllCoroutines();
            Destroy(gameObject);
        }

        public bool IsAnimating => _animating;
    }
}
