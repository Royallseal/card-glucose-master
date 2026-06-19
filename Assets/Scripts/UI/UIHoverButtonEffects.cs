using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using CGM.Core;

namespace CGM.UI
{
    /// <summary>
    /// 可复用的 UI 按钮 Hover 特效组件（支持略微放大与播放 Hover 提示音）。
    /// </summary>
    public class UIHoverButtonEffects : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("Hover 缩放特效")]
        [SerializeField] private float _hoverScale = 1.05f;
        [SerializeField] private float _duration = 0.1f;

        [Header("Hover 音效")]
        [SerializeField] private AudioClip _hoverSound;
        [Range(0f, 1f)]
        [SerializeField] private float _volume = 0.8f;

        [Header("目标 Transform (默认自身)")]
        [SerializeField] private Transform _targetTransform;

        private Vector3 _originalScale;
        private Coroutine _scaleCoroutine;

        private void Start()
        {
            if (_targetTransform == null)
            {
                _targetTransform = transform;
            }
            _originalScale = _targetTransform.localScale;

            // UI 按钮统一使用 Button_Hover 音效
            if (_hoverSound == null)
            {
                _hoverSound = Resources.Load<AudioClip>("Audio/Button_Hover");
            }
        }

        public void SetTargetTransform(Transform target)
        {
            _targetTransform = target;
            if (_targetTransform != null)
            {
                _originalScale = _targetTransform.localScale;
            }
        }

        private void OnDisable()
        {
            // 确保组件禁用时还原状态
            if (_scaleCoroutine != null)
            {
                StopCoroutine(_scaleCoroutine);
                _scaleCoroutine = null;
            }
            if (_targetTransform != null)
            {
                _targetTransform.localScale = _originalScale;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            // 检查是否为可交互状态（如果是 Button 组件且不可交互，则不响应）
            var btn = GetComponent<UnityEngine.UI.Button>();
            if (btn != null && !btn.interactable) return;

            StartScaleLerp(_originalScale * _hoverScale);
            PlayHoverSound();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            var btn = GetComponent<UnityEngine.UI.Button>();
            if (btn != null && !btn.interactable) return;

            StartScaleLerp(_originalScale);
        }

        /// <summary>
        /// 强制恢复原始大小
        /// </summary>
        public void ResetScale()
        {
            if (_scaleCoroutine != null)
            {
                StopCoroutine(_scaleCoroutine);
                _scaleCoroutine = null;
            }
            if (_targetTransform != null)
            {
                _targetTransform.localScale = _originalScale;
            }
        }

        private void StartScaleLerp(Vector3 target)
        {
            if (_scaleCoroutine != null)
            {
                StopCoroutine(_scaleCoroutine);
            }
            _scaleCoroutine = StartCoroutine(ScaleRoutine(target));
        }

        private IEnumerator ScaleRoutine(Vector3 target)
        {
            Vector3 start = _targetTransform != null ? _targetTransform.localScale : transform.localScale;
            float elapsed = 0f;
            while (elapsed < _duration)
            {
                elapsed += Time.deltaTime;
                if (_targetTransform != null)
                {
                    _targetTransform.localScale = Vector3.Lerp(start, target, elapsed / _duration);
                }
                yield return null;
            }
            if (_targetTransform != null)
            {
                _targetTransform.localScale = target;
            }
            _scaleCoroutine = null;
        }

        private void PlayHoverSound()
        {
            if (_hoverSound != null)
            {
                Vector3 pos = Camera.main != null ? Camera.main.transform.position : transform.position;
                float finalVolume = _volume * (AudioManager.Instance != null ? AudioManager.Instance.SfxVolume : 1f);
                AudioSource.PlayClipAtPoint(_hoverSound, pos, finalVolume);
            }
        }

        /// <summary>
        /// 程序化设置 Hover 音效和放大比例
        /// </summary>
        public void Setup(AudioClip sound, float scale = 1.05f)
        {
            _hoverSound = sound;
            _hoverScale = scale;
        }
    }
}
