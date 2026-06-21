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
        [SerializeField] private Button _startGameButton;
        [SerializeField] private Button _settingButton;
        [SerializeField] private Button _exitButton;

        [Header("面板引用")]
        [SerializeField] private GameObject _settingPanel;

        [Header("卡牌展开动画")]
        [Tooltip("CardArea 下的所有卡牌背景 RectTransform，按展开顺序排列（Blue, Red, Green, Purple）")]
        [SerializeField] private RectTransform[] _cardRects;

        [Tooltip("所有卡牌的展开起始锚点 — 即紫色卡的位置")]
        [SerializeField] private RectTransform _cardOriginAnchor;

        [Header("动画参数")]
        [SerializeField] private float _cardAnimDuration = 0.6f;
        [SerializeField] private float _initialDelay = 0f;

        // 缓存每张卡牌的最终 anchored position 和 rotation 及 alpha 值
        private Vector2[] _targetPositions;
        private Quaternion[] _targetRotations;
        private float[] _targetAlphas;

        private bool _isAwakeDone = false;

        private void Awake()
        {
            AutoResolveReferences();
            _isAwakeDone = true;
        }

        private void OnEnable()
        {
            // 确保 Awake 已完成引用解析后再播放动画
            if (!_isAwakeDone) return;

            // 停止所有残留的动画协程，避免与新动画冲突
            StopAllCoroutines();

            // 每次面板被激活时播放入场动画
            PlayCardFanAnimation();
        }

        private void Start()
        {
            // 绑定按钮事件
            if (_startGameButton != null)
            {
                _startGameButton.onClick.AddListener(OnStartGameClicked);
                AddHoverAlphaEffect(_startGameButton);
                AddButtonHoverSound(_startGameButton);
            }

            if (_settingButton != null)
            {
                _settingButton.onClick.AddListener(OnSettingClicked);
                AddHoverAlphaEffect(_settingButton);
                AddButtonHoverSound(_settingButton);
            }

            if (_exitButton != null)
            {
                _exitButton.onClick.AddListener(OnExitClicked);
                AddHoverAlphaEffect(_exitButton);
                AddButtonHoverSound(_exitButton);
            }
        }

        private void OnDestroy()
        {
            if (_startGameButton != null) _startGameButton.onClick.RemoveListener(OnStartGameClicked);
            if (_settingButton != null) _settingButton.onClick.RemoveListener(OnSettingClicked);
            if (_exitButton != null) _exitButton.onClick.RemoveListener(OnExitClicked);
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
            // 通过 SettingPanelController 打开设置（半透明覆盖在开始界面上方）
            if (_settingPanel != null)
            {
                var controller = _settingPanel.GetComponent<SettingPanelController>();
                if (controller == null)
                {
                    controller = _settingPanel.AddComponent<SettingPanelController>();
                }
                controller.Open(gameObject, fromStartingPanel: true);
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
            if (_cardRects == null || _cardRects.Length == 0) return;

            // 1. 缓存每张卡牌的当前位置、旋转和原始透明度（它们在 Inspector 中已被摆放到最终位置）
            if (_targetPositions == null || _targetPositions.Length != _cardRects.Length)
            {
                _targetPositions = new Vector2[_cardRects.Length];
                _targetRotations = new Quaternion[_cardRects.Length];
                _targetAlphas = new float[_cardRects.Length];
                for (int i = 0; i < _cardRects.Length; i++)
                {
                    _targetPositions[i] = _cardRects[i].anchoredPosition;
                    _targetRotations[i] = _cardRects[i].localRotation;
                    var img = _cardRects[i].GetComponent<Image>();
                    _targetAlphas[i] = img != null ? img.color.a : 0.9019608f;
                }
            }

            // 2. 获取起始位置和旋转（紫色卡的 anchored position 和 rotation）
            Vector2 originPos;
            Quaternion originRot;
            if (_cardOriginAnchor != null)
            {
                originPos = _cardOriginAnchor.anchoredPosition;
                originRot = _cardOriginAnchor.localRotation;
            }
            else
            {
                // 如果没有配置 anchor，取最后一张卡的位置作为起点
                int lastIdx = _cardRects.Length - 1;
                originPos = _targetPositions[lastIdx];
                originRot = _targetRotations[lastIdx];
            }

            // 3. 将所有卡牌先堆叠到起始位置，同时把需要飞行的卡牌初始透明度设为 0，防止重叠导致的透明度加深
            for (int i = 0; i < _cardRects.Length; i++)
            {
                _cardRects[i].anchoredPosition = originPos;
                _cardRects[i].localRotation = originRot;

                if (_cardRects[i] != _cardOriginAnchor)
                {
                    var img = _cardRects[i].GetComponent<Image>();
                    if (img != null)
                    {
                        Color c = img.color;
                        c.a = 0f;
                        img.color = c;
                    }
                }
            }

            // 4. 启动平滑插值动画协程
            StartCoroutine(FanOutSequence(originPos, originRot));
        }

        private IEnumerator FanOutSequence(Vector2 originPos, Quaternion originRot)
        {
            // 进入面板等待一定初始延迟，抵消初始化卡顿
            yield return new WaitForSeconds(_initialDelay);

            // 同时移动：一并启动所有卡牌的 AnimateCard 协程
            for (int i = 0; i < _cardRects.Length; i++)
            {
                StartCoroutine(AnimateCard(_cardRects[i], originPos, originRot,
                    _targetPositions[i], _targetRotations[i], _targetAlphas[i], _cardAnimDuration));
            }
        }

        private IEnumerator AnimateCard(RectTransform card, Vector2 fromPos, Quaternion fromRot,
            Vector2 toPos, Quaternion toRot, float targetAlpha, float duration)
        {
            var img = card.GetComponent<Image>();
            bool isMoving = Vector2.Distance(fromPos, toPos) > 0.1f;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // Ease-out cubic: 更自然的减速效果
                float eased = 1f - (1f - t) * (1f - t) * (1f - t);

                card.anchoredPosition = Vector2.Lerp(fromPos, toPos, eased);
                card.localRotation = Quaternion.Slerp(fromRot, toRot, eased);

                if (img != null && isMoving)
                {
                    Color c = img.color;
                    c.a = Mathf.Lerp(0f, targetAlpha, eased);
                    img.color = c;
                }

                yield return null;
            }

            // 确保精确到位
            card.anchoredPosition = toPos;
            card.localRotation = toRot;
            if (img != null)
            {
                Color c = img.color;
                c.a = targetAlpha;
                img.color = c;
            }
        }

        // =====================================================================
        // 自动解析引用
        // =====================================================================

        private void AutoResolveReferences()
        {
            // 按钮：ButtonArea 下的 StartGameButton, SettingButton, ExitButton
            if (_startGameButton == null)
            {
                Transform t = transform.Find("ButtonArea/StartGameButton");
                if (t != null) _startGameButton = t.GetComponent<Button>();
            }
            if (_settingButton == null)
            {
                Transform t = transform.Find("ButtonArea/SettingButton");
                if (t != null) _settingButton = t.GetComponent<Button>();
            }
            if (_exitButton == null)
            {
                Transform t = transform.Find("ButtonArea/ExitButton");
                if (t != null) _exitButton = t.GetComponent<Button>();
            }

            // 设置面板
            if (_settingPanel == null)
            {
                // 在 Canvas 同级或父级寻找 SettingPanel
                Transform canvas = transform.parent;
                if (canvas != null)
                {
                    Transform sp = canvas.Find("SettingPanel");
                    if (sp != null) _settingPanel = sp.gameObject;
                }
            }

            // 卡牌：CardArea 下的 Background_Card_* (按展开顺序)
            if (_cardRects == null || _cardRects.Length == 0)
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
                    _cardRects = list.ToArray();
                }
            }

            // 卡牌展开起始锚点 = 紫色卡
            if (_cardOriginAnchor == null && _cardRects != null && _cardRects.Length > 0)
            {
                // 紫色卡是最后一张
                _cardOriginAnchor = _cardRects[_cardRects.Length - 1];
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
