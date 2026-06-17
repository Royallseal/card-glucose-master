using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CGM.UI
{
    /// <summary>
    /// 可复用的 UI 按钮 Hover 特效组件（支持略微放大与播放 Hover 提示音）。
    /// </summary>
    public class UIHoverButtonEffects : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Hover 缩放特效")]
        [SerializeField] private float hoverScale = 1.05f;
        [SerializeField] private float duration = 0.1f;

        [Header("Hover 音效")]
        [SerializeField] private AudioClip hoverSound;
        [Range(0f, 1f)]
        [SerializeField] private float volume = 0.8f;

        private Vector3 originalScale;
        private Coroutine scaleCoroutine;

        private void Start()
        {
            originalScale = transform.localScale;
        }

        private void OnDisable()
        {
            // 确保组件禁用时还原状态
            if (scaleCoroutine != null)
            {
                StopCoroutine(scaleCoroutine);
                scaleCoroutine = null;
            }
            transform.localScale = originalScale;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            // 检查是否为可交互状态（如果是 Button 组件且不可交互，则不响应）
            var btn = GetComponent<UnityEngine.UI.Button>();
            if (btn != null && !btn.interactable) return;

            StartScaleLerp(originalScale * hoverScale);
            PlayHoverSound();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            var btn = GetComponent<UnityEngine.UI.Button>();
            if (btn != null && !btn.interactable) return;

            StartScaleLerp(originalScale);
        }

        /// <summary>
        /// 强制恢复原始大小
        /// </summary>
        public void ResetScale()
        {
            if (scaleCoroutine != null)
            {
                StopCoroutine(scaleCoroutine);
                scaleCoroutine = null;
            }
            transform.localScale = originalScale;
        }

        private void StartScaleLerp(Vector3 target)
        {
            if (scaleCoroutine != null)
            {
                StopCoroutine(scaleCoroutine);
            }
            scaleCoroutine = StartCoroutine(ScaleRoutine(target));
        }

        private IEnumerator ScaleRoutine(Vector3 target)
        {
            Vector3 start = transform.localScale;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                transform.localScale = Vector3.Lerp(start, target, elapsed / duration);
                yield return null;
            }
            transform.localScale = target;
            scaleCoroutine = null;
        }

        private void PlayHoverSound()
        {
            if (hoverSound != null)
            {
                // 使用 PlayClipAtPoint 在相机位置播放音效，实现全局 UI 点击/悬停音效反馈
                Vector3 pos = Camera.main != null ? Camera.main.transform.position : transform.position;
                AudioSource.PlayClipAtPoint(hoverSound, pos, volume);
            }
        }

        /// <summary>
        /// 程序化设置 Hover 音效和放大比例
        /// </summary>
        public void Setup(AudioClip sound, float scale = 1.05f)
        {
            hoverSound = sound;
            hoverScale = scale;
        }
    }
}
