// =============================================================================
// StartingPanelController.cs — 开始界面控制器
// 命名空间：CGM.UI
// 职责：管理开始界面的按钮逻辑、Hover 透明度动效以及卡牌扇形展开入场动画。
// =============================================================================

using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CGM.UI
{
    /// <summary>
    /// 开始界面总控脚本，挂载在 StartingPanel 根节点上。
    /// </summary>
    public class StartingPanelController : MonoBehaviour
    {
        [Header("按钮引用")]
        [SerializeField] private Button startGameButton;
        [SerializeField] private Button settingButton;
        [SerializeField] private Button exitButton;

        [Header("面板引用")]
        [SerializeField] private GameObject settingPanel;

        [Header("卡牌展开动画")]
        [Tooltip("CardArea 下的所有卡牌背景 RectTransform，按展开顺序排列（Blue, Red, Green, Purple）")]
        [SerializeField] private RectTransform[] cardRects;

        [Tooltip("所有卡牌的展开起始锚点 — 即紫色卡的位置")]
        [SerializeField] private RectTransform cardOriginAnchor;

        [Header("动画参数")]
        [SerializeField] private float cardAnimDuration = 0.6f;
        [SerializeField] private float cardStaggerDelay = 0.12f;

        // 缓存每张卡牌的最终 anchored position 和 rotation
        private Vector2[] targetPositions;
        private Quaternion[] targetRotations;

        private bool isAwakeDone = false;

        private void Awake()
        {
            AutoResolveReferences();
            isAwakeDone = true;
        }

        private void OnEnable()
        {
            // 确保 Awake 已完成引用解析后再播放动画
            if (!isAwakeDone) return;

            // 停止所有残留的动画协程，避免与新动画冲突
            StopAllCoroutines();

            // 每次面板被激活时播放入场动画
            PlayCardFanAnimation();
        }

        private void Start()
        {
            // 绑定按钮事件
            if (startGameButton != null)
            {
                startGameButton.onClick.AddListener(OnStartGameClicked);
                AddHoverAlphaEffect(startGameButton);
                AddButtonHoverSound(startGameButton);
            }

            if (settingButton != null)
            {
                settingButton.onClick.AddListener(OnSettingClicked);
                AddHoverAlphaEffect(settingButton);
                AddButtonHoverSound(settingButton);
            }

            if (exitButton != null)
            {
                exitButton.onClick.AddListener(OnExitClicked);
                AddHoverAlphaEffect(exitButton);
                AddButtonHoverSound(exitButton);
            }
        }

        private void OnDestroy()
        {
            if (startGameButton != null) startGameButton.onClick.RemoveListener(OnStartGameClicked);
            if (settingButton != null) settingButton.onClick.RemoveListener(OnSettingClicked);
            if (exitButton != null) exitButton.onClick.RemoveListener(OnExitClicked);
        }

        // =====================================================================
        // 按钮回调
        // =====================================================================

        private void OnStartGameClicked()
        {
            var gsm = Core.GameSessionManager.Instance;
            if (gsm != null)
            {
                gsm.StartGame();
            }
        }

        private void OnSettingClicked()
        {
            if (settingPanel != null)
            {
                settingPanel.SetActive(!settingPanel.activeSelf);
            }
        }

        private void OnExitClicked()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // =====================================================================
        // Hover 透明度动效：鼠标进入时 alpha 230→255，离开时 255→230
        // =====================================================================

        private void AddHoverAlphaEffect(Button btn)
        {
            var trigger = btn.gameObject.GetComponent<EventTrigger>();
            if (trigger == null) trigger = btn.gameObject.AddComponent<EventTrigger>();

            // Pointer Enter → 不透明
            var entryEnter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            Image img = btn.GetComponent<Image>();
            entryEnter.callback.AddListener((_) =>
            {
                if (img != null) SetImageAlpha(img, 1.0f); // 255
            });
            trigger.triggers.Add(entryEnter);

            // Pointer Exit → 半透明
            var entryExit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            entryExit.callback.AddListener((_) =>
            {
                if (img != null) SetImageAlpha(img, 230f / 255f); // 230
            });
            trigger.triggers.Add(entryExit);

            // 确保初始状态为 230/255
            if (img != null) SetImageAlpha(img, 230f / 255f);
        }

        private void SetImageAlpha(Image img, float alpha)
        {
            Color c = img.color;
            c.a = alpha;
            img.color = c;
        }

        /// <summary>
        /// 为按钮添加 UIHoverButtonEffects 组件并配置 Button_Hover 音效
        /// </summary>
        private void AddButtonHoverSound(Button btn)
        {
            var hover = btn.gameObject.GetComponent<UIHoverButtonEffects>();
            if (hover == null) hover = btn.gameObject.AddComponent<UIHoverButtonEffects>();
            AudioClip clip = Resources.Load<AudioClip>("Audio/Button_Hover");
            hover.Setup(clip, 1.0f); // 按钮不额外缩放，透明度变化已由 AddHoverAlphaEffect 处理
        }

        // =====================================================================
        // 卡牌扇形展开入场动画
        // =====================================================================

        private void PlayCardFanAnimation()
        {
            if (cardRects == null || cardRects.Length == 0) return;

            // 1. 缓存每张卡牌的当前位置和旋转（它们在 Inspector 中已被摆放到最终位置）
            if (targetPositions == null || targetPositions.Length != cardRects.Length)
            {
                targetPositions = new Vector2[cardRects.Length];
                targetRotations = new Quaternion[cardRects.Length];
                for (int i = 0; i < cardRects.Length; i++)
                {
                    targetPositions[i] = cardRects[i].anchoredPosition;
                    targetRotations[i] = cardRects[i].localRotation;
                }
            }

            // 2. 获取起始位置和旋转（紫色卡的 anchored position 和 rotation）
            Vector2 originPos;
            Quaternion originRot;
            if (cardOriginAnchor != null)
            {
                originPos = cardOriginAnchor.anchoredPosition;
                originRot = cardOriginAnchor.localRotation;
            }
            else
            {
                // 如果没有配置 anchor，取最后一张卡的位置作为起点
                int lastIdx = cardRects.Length - 1;
                originPos = targetPositions[lastIdx];
                originRot = targetRotations[lastIdx];
            }

            // 3. 将所有卡牌先堆叠到起始位置
            for (int i = 0; i < cardRects.Length; i++)
            {
                cardRects[i].anchoredPosition = originPos;
                cardRects[i].localRotation = originRot;
            }

            // 4. 依次启动每张卡牌的平滑插值动画
            StartCoroutine(FanOutSequence(originPos, originRot));
        }

        private IEnumerator FanOutSequence(Vector2 originPos, Quaternion originRot)
        {
            for (int i = 0; i < cardRects.Length; i++)
            {
                StartCoroutine(AnimateCard(cardRects[i], originPos, originRot,
                    targetPositions[i], targetRotations[i], cardAnimDuration));

                // 在下一张卡牌开始前等待交错延迟
                if (i < cardRects.Length - 1)
                {
                    yield return new WaitForSeconds(cardStaggerDelay);
                }
            }
        }

        private IEnumerator AnimateCard(RectTransform card, Vector2 fromPos, Quaternion fromRot,
            Vector2 toPos, Quaternion toRot, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // Ease-out cubic: 更自然的减速效果
                float eased = 1f - (1f - t) * (1f - t) * (1f - t);

                card.anchoredPosition = Vector2.Lerp(fromPos, toPos, eased);
                card.localRotation = Quaternion.Slerp(fromRot, toRot, eased);

                yield return null;
            }

            // 确保精确到位
            card.anchoredPosition = toPos;
            card.localRotation = toRot;
        }

        // =====================================================================
        // 自动解析引用
        // =====================================================================

        private void AutoResolveReferences()
        {
            // 按钮：ButtonArea 下的 StartGameButton, SettingButton, ExitButton
            if (startGameButton == null)
            {
                Transform t = transform.Find("ButtonArea/StartGameButton");
                if (t != null) startGameButton = t.GetComponent<Button>();
            }
            if (settingButton == null)
            {
                Transform t = transform.Find("ButtonArea/SettingButton");
                if (t != null) settingButton = t.GetComponent<Button>();
            }
            if (exitButton == null)
            {
                Transform t = transform.Find("ButtonArea/ExitButton");
                if (t != null) exitButton = t.GetComponent<Button>();
            }

            // 设置面板
            if (settingPanel == null)
            {
                // 在 Canvas 同级或父级寻找 SettingPanel
                Transform canvas = transform.parent;
                if (canvas != null)
                {
                    Transform sp = canvas.Find("SettingPanel");
                    if (sp != null) settingPanel = sp.gameObject;
                }
            }

            // 卡牌：CardArea 下的 Background_Card_* (按展开顺序)
            if (cardRects == null || cardRects.Length == 0)
            {
                Transform cardArea = transform.Find("Background/CardArea");
                if (cardArea != null)
                {
                    // 按固定名称顺序排列：Blue(最左) → Red → Green → Purple(不动)
                    string[] cardNames = { "Background_Card_Blue", "Background_Card_Red",
                                           "Background_Card_Green", "Background_Card_Purple" };
                    var list = new System.Collections.Generic.List<RectTransform>();
                    foreach (string name in cardNames)
                    {
                        Transform ct = cardArea.Find(name);
                        if (ct != null)
                        {
                            list.Add(ct.GetComponent<RectTransform>());
                        }
                    }
                    cardRects = list.ToArray();
                }
            }

            // 卡牌展开起始锚点 = 紫色卡
            if (cardOriginAnchor == null && cardRects != null && cardRects.Length > 0)
            {
                // 紫色卡是最后一张
                cardOriginAnchor = cardRects[cardRects.Length - 1];
            }
        }

#if UNITY_EDITOR
        private void Reset()
        {
            AutoResolveReferences();
        }
#endif
    }
}
