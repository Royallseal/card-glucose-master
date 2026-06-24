// =============================================================================
// CreditsPanelController.cs — 鸣谢界面控制器
// 命名空间：CGM.UI
// 职责：从 credits.json 加载署名文本，动态填充 ScrollView，提供返回功能。
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CGM.UI
{
    /// <summary>
    /// 鸣谢界面控制器。从 Resources/Configs/credits.json 读取文本行，
    /// 逐行动态实例化 TextPrefab 填充到 ScrollView 的 Content 容器中。
    /// </summary>
    public class CreditsPanelController : MonoBehaviour
    {
        [Header("UI 引用")]
        [SerializeField] private Transform contentContainer;    // ScrollView 的 Content
        [SerializeField] private GameObject textLinePrefab;     // 单行文本预制体（含 TextMeshProUGUI）
        [SerializeField] private Button backButton;

        [Header("动画")]
        [SerializeField] private float animDuration = 0.25f;

        private GameObject sourcePanel;
        private bool isOpen;
        private Coroutine animCoroutine;
        private CanvasGroup canvasGroup;

        private void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        private void Start()
        {
            if (backButton != null)
                backButton.onClick.AddListener(Close);
        }

        private void OnDestroy()
        {
            if (backButton != null)
                backButton.onClick.RemoveListener(Close);
        }

        // =====================================================================
        // 公开接口
        // =====================================================================

        /// <summary>
        /// 打开鸣谢界面，从来源面板进入。
        /// </summary>
        public void Open(GameObject srcPanel)
        {
            if (isOpen) return;
            sourcePanel = srcPanel;
            isOpen = true;

            gameObject.SetActive(true);
            LoadCredits();
            PlayOpenAnimation();
        }

        /// <summary>
        /// 关闭鸣谢界面，返回来源面板。
        /// </summary>
        public void Close()
        {
            if (!isOpen) return;
            isOpen = false;

            if (animCoroutine != null)
                StopCoroutine(animCoroutine);
            animCoroutine = StartCoroutine(CloseAnimationRoutine());
        }

        // =====================================================================
        // 数据加载
        // =====================================================================

        private void LoadCredits()
        {
            // 清除已有子对象（避免重复加载）
            foreach (Transform child in contentContainer)
            {
                Destroy(child.gameObject);
            }

            // 加载 JSON
            var wrapper = Resources.Load<TextAsset>("Configs/credits");
            if (wrapper == null)
            {
                Debug.LogWarning("[CreditsPanel] 未找到 Resources/Configs/credits.json");
                return;
            }

            var lines = JsonUtility.FromJson<CreditsData>(wrapper.text);
            if (lines == null || lines.lines == null || lines.lines.Length == 0)
            {
                Debug.LogWarning("[CreditsPanel] credits.json 数据为空");
                return;
            }

            // 动态生成文本行
            foreach (var line in lines.lines)
            {
                GameObject go = Instantiate(textLinePrefab, contentContainer);
                var tmp = go.GetComponentInChildren<TextMeshProUGUI>();
                if (tmp != null)
                {
                    tmp.text = string.IsNullOrEmpty(line.text) ? " " : line.text;
                }
            }
        }

        // =====================================================================
        // 动画
        // =====================================================================

        private void PlayOpenAnimation()
        {
            if (animCoroutine != null)
                StopCoroutine(animCoroutine);
            animCoroutine = StartCoroutine(FadeInRoutine());
        }

        private System.Collections.IEnumerator FadeInRoutine()
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = true;
            float elapsed = 0f;
            while (elapsed < animDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Clamp01(elapsed / animDuration);
                yield return null;
            }
            canvasGroup.alpha = 1f;
            animCoroutine = null;
        }

        private System.Collections.IEnumerator CloseAnimationRoutine()
        {
            float startAlpha = canvasGroup.alpha;
            float elapsed = 0f;
            while (elapsed < animDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, elapsed / animDuration);
                yield return null;
            }
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            gameObject.SetActive(false);

            // 恢复来源面板
            if (sourcePanel != null)
                sourcePanel.SetActive(true);

            animCoroutine = null;
        }

        // =====================================================================
        // 数据模型
        // =====================================================================

        [System.Serializable]
        private class CreditsData
        {
            public CreditsLine[] lines;
        }

        [System.Serializable]
        private class CreditsLine
        {
            public string text;
        }
    }
}
