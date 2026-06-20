// =============================================================================
// BattleHandDisplay.cs — 手牌 UI 桥接器
// 命名空间：CGM.UI
// 职责：订阅 BattleSessionController 事件，自动刷新卡牌、能量、回合状态等 UI。
//       同时为每张手牌挂载点击出牌回调。
// =============================================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CGM.Core;
using CGM.Data;

namespace CGM.UI
{
    /// <summary>
    /// 战斗 UI 总控桥接器。
    /// 挂载在 Canvas 下的一个空对象上，把所有 UI 引用拖好后即可自动运转。
    /// </summary>
    public class BattleHandDisplay : MonoBehaviour
    {
        [Header("战斗中枢引用")]
        [SerializeField] private BattleSessionController battleController;

        [Header("手牌区域")]
        [SerializeField] private Transform handContainer;
        [SerializeField] private GameObject cardPrefab;
        [Tooltip("弃牌堆（弃牌动画终点）")]
        [SerializeField] private RectTransform discardPileTarget;
        [Tooltip("抽牌堆（抽牌动画起点）")]
        [SerializeField] private RectTransform drawPileTarget;

        [Header("状态文本")]
        [Tooltip("显示当前回合阶段")]
        [SerializeField] private TextMeshProUGUI phaseText;
        [Tooltip("显示当前能量（如 3/3）")]
        [SerializeField] private TextMeshProUGUI energyText;
        [Tooltip("抽牌堆计数（DrawPile_UI 的子 Text）")]
        [SerializeField] private TextMeshProUGUI drawPileCountText;
        [Tooltip("弃牌堆计数（DiscardPile_UI 的子 Text）")]
        [SerializeField] private TextMeshProUGUI discardPileCountText;

        [Header("按钮")]
        [Tooltip("结束回合按钮")]
        [SerializeField] private Button endTurnButton;
        [Tooltip("结束回合按钮的文本（用于置灰反馈）")]
        [SerializeField] private TextMeshProUGUI endTurnButtonText;

        [Header("回合计数")]
        [Tooltip("回合计数器文本 (BattlePanel/RoundCound/RoundCoundText)")]
        [SerializeField] private TextMeshProUGUI roundCountText;

        [Header("特效锁定")]
        [SerializeField] private BattleEffectController effectController;

        // 当前手牌对象池
        private readonly List<GameObject> handObjects = new List<GameObject>();

        // 视觉计数与目标计数（用于实现随动画逐张更新）
        private int visualDrawCount = -1;
        private int visualDiscardCount = -1;
        private int targetDrawCount = 0;
        private int targetDiscardCount = 0;

        // 动画状态追踪
        private bool isDrawingCards = false;
        private int activeDiscardAnimations = 0;

        // 溢出爆牌排队系统
        private readonly Queue<CardInfo> overflowQueue = new Queue<CardInfo>();
        private bool isProcessingOverflow = false;
        private int activeOverflowAnimations = 0;

        /// <summary>
        /// 当前手牌区是否正在播放抽牌或弃牌动画。
        /// </summary>
        public bool IsAnimating => isDrawingCards || activeDiscardAnimations > 0 || isProcessingOverflow || activeOverflowAnimations > 0;

        private void Awake()
        {
            // 清理 HandContainer 下的所有初始子物体（设计时占位符），在 Awake 中完成以确保在 StartBattle 之前
            if (handContainer != null)
            {
                foreach (Transform child in handContainer)
                {
                    child.SetParent(null);
                    Destroy(child.gameObject);
                }
            }

            // 自动查找回合计数器
            if (roundCountText == null)
            {
                var battlePanel = GetComponentInParent<Canvas>()?.transform.Find("BattlePanel");
                if (battlePanel == null)
                {
                    var allCanvases = FindObjectsOfType<Canvas>();
                    foreach (var c in allCanvases)
                    {
                        var bp = c.transform.Find("BattlePanel");
                        if (bp != null) { battlePanel = bp; break; }
                    }
                }
                if (battlePanel != null)
                {
                    var rc = battlePanel.Find("RoundCound/RoundCoundText");
                    if (rc != null) roundCountText = rc.GetComponent<TextMeshProUGUI>();
                }
            }

            // 自动查找特效控制器
            if (effectController == null)
                effectController = FindObjectOfType<BattleEffectController>();
        }

        private void Start()
        {
            if (battleController == null)
            {
                battleController = FindObjectOfType<BattleSessionController>();
            }

            if (battleController == null)
            {
                Debug.LogError("[BattleHandDisplay] 未找到 BattleSessionController！请在 Inspector 中拖入引用。");
                return;
            }

            // =====================================================================
            // 订阅所有战斗事件
            // =====================================================================

            // 手牌变化 → 刷新手牌 UI
            battleController.OnHandChanged += OnHandChanged;

            // 能量变化 → 更新能量文本
            battleController.OnEnergyChanged += OnEnergyChanged;

            // 回合阶段变化 → 更新阶段文本、控制按钮
            battleController.OnPhaseChanged += OnPhaseChanged;

            // 牌堆变化 → 更新计数
            battleController.OnPilesChanged += OnPilesChanged;

            // 出牌完成 → 可选特效
            battleController.OnCardPlayed += OnCardPlayed;

            // 战斗结束 → 显示结果
            battleController.OnBattleEnded += OnBattleEnded;

            // 状态警告 → 弹提示
            battleController.OnStateWarning += OnStateWarning;

            // 摸牌溢出 → 播放爆牌动画
            battleController.OnCardOverflowed += OnCardOverflowed;

            // 战斗日志 → Debug 输出
            battleController.OnCombatLog += OnCombatLog;

            // 订阅玩家属性变化，实现手牌数值与描述的实时刷新
            if (battleController.PlayerStats != null)
            {
                battleController.PlayerStats.OnStatsChanged += RefreshHandCardVisuals;
                battleController.PlayerStats.OnGlucoseChanged += RefreshHandCardVisualsWithGlucose;
            }

            // =====================================================================
            // 按钮绑定
            // =====================================================================
            if (endTurnButton != null)
            {
                endTurnButton.onClick.AddListener(OnEndTurnClicked);
            }

            // 动态配置抽牌堆与弃牌堆的 Hover 特效和音效
            if (drawPileTarget != null)
            {
                var h = drawPileTarget.gameObject.GetComponent<UIHoverButtonEffects>();
                if (h == null) h = drawPileTarget.gameObject.AddComponent<UIHoverButtonEffects>();
                h.Setup(Resources.Load<AudioClip>("Audio/Button_Hover"), 1.05f);
            }
            if (discardPileTarget != null)
            {
                var h = discardPileTarget.gameObject.GetComponent<UIHoverButtonEffects>();
                if (h == null) h = discardPileTarget.gameObject.AddComponent<UIHoverButtonEffects>();
                h.Setup(Resources.Load<AudioClip>("Audio/Button_Hover"), 1.05f);
            }

            // 初始刷新一次（如果战斗已经开始）
            OnPhaseChanged(battleController.Phase);
            OnEnergyChanged(battleController.CurrentEnergy, battleController.MaxEnergy);
        }

        private void OnDestroy()
        {
            // 注销所有事件，防止内存泄漏
            if (battleController != null)
            {
                battleController.OnHandChanged -= OnHandChanged;
                battleController.OnEnergyChanged -= OnEnergyChanged;
                battleController.OnPhaseChanged -= OnPhaseChanged;
                battleController.OnPilesChanged -= OnPilesChanged;
                battleController.OnCardPlayed -= OnCardPlayed;
                battleController.OnBattleEnded -= OnBattleEnded;
                battleController.OnStateWarning -= OnStateWarning;
                battleController.OnCardOverflowed -= OnCardOverflowed;
                battleController.OnCombatLog -= OnCombatLog;

                if (battleController.PlayerStats != null)
                {
                    battleController.PlayerStats.OnStatsChanged -= RefreshHandCardVisuals;
                    battleController.PlayerStats.OnGlucoseChanged -= RefreshHandCardVisualsWithGlucose;
                }
            }
        }

        private void Update()
        {
            UpdateEndTurnButton();
            RefreshHandInteractable();
        }

        /// <summary>
        /// 统一根据当前回合阶段、战斗特效、以及手牌动画状态，动态更新结束回合按钮的 interactable。
        /// </summary>
        private void UpdateEndTurnButton()
        {
            if (endTurnButton == null) return;

            bool isPlayerTurn = battleController != null && battleController.Phase == BattleTurnPhase.PlayerTurn;
            bool effectLocked = effectController != null && effectController.IsPlayingEffect;
            bool animating = IsAnimating;

            endTurnButton.interactable = isPlayerTurn && !effectLocked && !animating;
        }

        // =========================================================================
        // 事件回调：手牌变化
        // =========================================================================
        private void OnHandChanged(IReadOnlyList<CardInfo> handCards)
        {
            // 记下变动前所有卡牌位置
            var oldPositions = new Dictionary<GameObject, Vector2>();
            foreach (var go in handObjects)
            {
                if (go != null) oldPositions[go] = go.GetComponent<RectTransform>().position;
            }

            // ── 优先清理被拖拽打出的卡牌 ──────────────────────────────────────
            // 必须在 ID 匹配之前处理：同名卡牌（如多张 starter_rice）会导致
            // 被打出的那张被误判为「还在手牌中」，而另一张无辜留存的牌反而
            // 触发弃牌动画。直接找到有 DraggedAndPlayed 标记的对象并立即销毁。
            for (int i = handObjects.Count - 1; i >= 0; i--)
            {
                var go = handObjects[i];
                if (go == null) { handObjects.RemoveAt(i); continue; }
                var dh = go.GetComponent<CardDragHandler>();
                if (dh != null && dh.DraggedAndPlayed)
                {
                    handObjects.RemoveAt(i);
                    go.transform.SetParent(null); // 立即脱离父物体，避免影响随后的 Layout 排版计算导致闪烁
                    Destroy(go);
                    // 出牌立刻使弃牌堆视觉计数+1（不超过目标数）
                    if (visualDiscardCount < targetDiscardCount)
                    {
                        visualDiscardCount++;
                        UpdateCountTexts();
                    }
                }
            }

            var remainIds = new List<string>();
            if (handCards != null)
                foreach (var c in handCards) if (c != null) remainIds.Add(c.id);

            for (int i = handObjects.Count - 1; i >= 0; i--)
            {
                var go = handObjects[i];
                if (go == null) { handObjects.RemoveAt(i); continue; }
                string cardId = go.name.Replace("HandCard_", "");
                int idx = remainIds.IndexOf(cardId);
                if (idx >= 0)
                {
                    remainIds.RemoveAt(idx);
                }
                else
                {
                    handObjects.RemoveAt(i);
                    var anim = go.GetComponent<CardAnimator>();
                    if (anim != null && discardPileTarget != null)
                    {
                        // 移出 handContainer，避免影响布局计算
                        go.transform.SetParent(discardPileTarget.parent, true);
                        activeDiscardAnimations++;
                        anim.PlayDiscardAnimation(discardPileTarget.position, () => {
                            activeDiscardAnimations--;
                            // 弃牌飞完时，视觉计数+1
                            if (visualDiscardCount < targetDiscardCount)
                            {
                                visualDiscardCount++;
                                UpdateCountTexts();
                            }
                            CheckAndSyncCounters();
                        });
                    }
                    else
                    {
                        Destroy(go);
                    }
                }
            }

            // 新卡批量加入
            if (remainIds.Count > 0 && cardPrefab != null && handContainer != null)
            {
                var toAdd = new List<CardInfo>();
                if (handCards != null)
                {
                    foreach (var card in handCards)
                    {
                        if (card == null) continue;
                        int idx = remainIds.IndexOf(card.id);
                        if (idx >= 0) { toAdd.Add(card); remainIds.RemoveAt(idx); }
                    }
                }
                if (toAdd.Count > 0)
                    StartCoroutine(BatchAddRoutine(toAdd));
            }

            // 剩余卡牌平滑移动到新位置
            StartCoroutine(SmoothRemaining(oldPositions));
        }

        private IEnumerator SmoothRemaining(Dictionary<GameObject, Vector2> oldPositions)
        {
            // 强制重新计算排版，使布局组立即更新所有子物体的目标位置
            if (handContainer != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(handContainer.GetComponent<RectTransform>());
            }

            // 缓存好静止的目标排版位置，避免在插值循环中读取正在移动的位置导致计算退化
            var targetPositions = new Dictionary<GameObject, Vector2>();
            foreach (var go in handObjects)
            {
                if (go != null)
                {
                    targetPositions[go] = go.GetComponent<RectTransform>().position;
                }
            }

            float duration = 0.15f;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / duration);
                float eased = 1f - (1f - p) * (1f - p); // Ease-out quad
                
                foreach (var go in handObjects)
                {
                    if (go == null || !oldPositions.ContainsKey(go) || !targetPositions.ContainsKey(go)) continue;
                    Vector2 oldPos = oldPositions[go];
                    Vector2 targetPos = targetPositions[go];
                    go.GetComponent<RectTransform>().position = Vector2.Lerp(oldPos, targetPos, eased);
                }
                yield return null;
            }
            // 最终让 LayoutGroup 接管
            if (handContainer != null)
            {
                LayoutRebuilder.MarkLayoutForRebuild(handContainer.GetComponent<RectTransform>());
            }
        }

        private IEnumerator BatchAddRoutine(List<CardInfo> cards)
        {
            isDrawingCards = true;
            // ⚠️ drawPileTarget 未赋值时退回到屏幕正下方不可见区域，避免卡牌从可见的 (0,0) 飞入产生虚影
            //    请在 Inspector 中把抽牌堆 UI 拖到 BattleHandDisplay.drawPileTarget 字段！
            if (drawPileTarget == null)
                Debug.LogWarning("[BattleHandDisplay] drawPileTarget 未赋值！抽牌动画起点将使用屏幕外备用位置。请在 Inspector 中拖入 DrawPile_UI。");
            Vector2 startPos = drawPileTarget != null
                ? (Vector2)drawPileTarget.position
                : new Vector2(Screen.width * 0.5f, -300f); // 屏幕正下方不可见区域

            foreach (var card in cards)
            {
                // 卡牌开始飞出的瞬间，使抽牌堆视觉计数-1（但不低于目标值）
                if (visualDrawCount > targetDrawCount)
                {
                    visualDrawCount--;
                    UpdateCountTexts();
                }

                // 1. 实例化一张
                GameObject cardGo = Instantiate(cardPrefab, handContainer);
                cardGo.name = $"HandCard_{card.id}";
                var anim = cardGo.GetComponent<CardAnimator>();
                if (anim == null) anim = cardGo.AddComponent<CardAnimator>();

                var cardUI = cardGo.GetComponent<CardUI>();
                if (cardUI != null)
                {
                    SetCardRealTimeValues(cardUI, card);
                }

                var dragHandler = cardGo.GetComponent<CardDragHandler>();
                if (dragHandler == null) dragHandler = cardGo.AddComponent<CardDragHandler>();
                dragHandler.SetCardInfo(card);

                var btn = cardGo.GetComponent<Button>();
                if (btn == null) btn = cardGo.AddComponent<Button>();
                btn.onClick.RemoveAllListeners();
                CardInfo captured = card;
                btn.onClick.AddListener(() => OnCardClicked(captured));
                btn.interactable = battleController.CanPlayCard(card);

                handObjects.Add(cardGo);

                // 2. 获取卡牌在 handContainer 中应有的最终世界坐标
                LayoutRebuilder.ForceRebuildLayoutImmediate(handContainer.GetComponent<RectTransform>());
                Vector2 endPos = cardGo.GetComponent<RectTransform>().position;

                // 3. 把卡牌移出 LayoutGroup 的管辖范围，避免 Layout 每帧覆盖动画位置
                //    保持世界坐标不变，挂到 Canvas 根节点下，初始 alpha=0
                var canvas = GetComponentInParent<Canvas>()?.rootCanvas;
                var canvasRoot = canvas != null ? canvas.transform : transform.parent;
                var cg = cardGo.GetComponent<CanvasGroup>();
                if (cg == null) cg = cardGo.AddComponent<CanvasGroup>();
                cg.alpha = 0f;
                cardGo.transform.SetParent(canvasRoot, true);

                // 4. 从抽牌堆飞入，动画结束后自动移回 handContainer
                anim.PlayDrawAnimation(startPos, endPos, handContainer);

                // 5. 等一段时间再抽下一张
                yield return new WaitForSeconds(0.15f);
            }
            isDrawingCards = false;
            CheckAndSyncCounters();
        }

        // =========================================================================
        // 事件回调：能量变化
        // =========================================================================
        private void OnEnergyChanged(int current, int max)
        {
            if (energyText != null)
            {
                energyText.text = $"{current}/{max}";
            }

            // 刷新所有手牌的可用状态
            RefreshHandInteractable();
        }

        // =========================================================================
        // 事件回调：回合阶段变化
        // =========================================================================
        private void OnPhaseChanged(BattleTurnPhase phase)
        {
            if (phase == BattleTurnPhase.NotStarted)
            {
                visualDrawCount = -1;
                visualDiscardCount = -1;
            }

            if (phaseText != null)
            {
                phaseText.text = phase switch
                {
                    BattleTurnPhase.NotStarted => "准备中...",
                    BattleTurnPhase.PlayerTurn => "你的回合",
                    BattleTurnPhase.EnemyTurn => "敌人回合",
                    BattleTurnPhase.Victory => "胜利！",
                    BattleTurnPhase.Defeat => "失败",
                    _ => ""
                };
            }

            // 只有玩家回合且不在播特效时才能点结束回合
            UpdateEndTurnButton();

            // 更新回合计数（TurnNumber 表示当前第几个玩家回合，即第几回合）
            if (roundCountText != null && battleController != null &&
                phase != BattleTurnPhase.NotStarted)
            {
                roundCountText.text = $"第{battleController.TurnNumber}回合";
            }

            RefreshHandInteractable();
        }

        private void OnPilesChanged(IReadOnlyList<CardInfo> hand, IReadOnlyList<CardInfo> draw, IReadOnlyList<CardInfo> discard)
        {
            int nextDraw = draw?.Count ?? 0;
            int nextDiscard = discard?.Count ?? 0;

            // 首次赋值初始化
            if (visualDrawCount == -1 || visualDiscardCount == -1)
            {
                visualDrawCount = nextDraw;
                visualDiscardCount = nextDiscard;
                UpdateCountTexts();
                
                targetDrawCount = nextDraw;
                targetDiscardCount = nextDiscard;
                return;
            }

            // 如果发生洗牌（在正常一局游戏中，弃牌堆减少的唯一可能就是触发了“洗牌”将弃牌堆全部移回抽牌堆）
            if (nextDiscard < targetDiscardCount)
            {
                visualDrawCount += targetDiscardCount;
                visualDiscardCount = nextDiscard;
                UpdateCountTexts();
            }

            targetDrawCount = nextDraw;
            targetDiscardCount = nextDiscard;

            // 如果当前没有任何动画在播放，直接同步（兜底）
            if (!isDrawingCards && activeDiscardAnimations == 0)
            {
                visualDrawCount = nextDraw;
                visualDiscardCount = nextDiscard;
                UpdateCountTexts();
            }
        }

        /// <summary>
        /// 更新 UI 上的抽牌堆和弃牌堆计数文本
        /// </summary>
        private void UpdateCountTexts()
        {
            if (drawPileCountText != null)
            {
                drawPileCountText.text = visualDrawCount.ToString();
            }
            if (discardPileCountText != null)
            {
                discardPileCountText.text = visualDiscardCount.ToString();
            }
        }

        /// <summary>
        /// 检查当前是否在播放动画，在完全没有动画播放时，把视觉数值对齐到目标最新真实数值（兜底安全逻辑）
        /// </summary>
        private void CheckAndSyncCounters()
        {
            if (!isDrawingCards && activeDiscardAnimations == 0)
            {
                visualDrawCount = targetDrawCount;
                visualDiscardCount = targetDiscardCount;
                UpdateCountTexts();
            }
        }

        // =========================================================================
        // 事件回调：出牌完成
        // =========================================================================
        private void OnCardPlayed(CardPlayResult result)
        {
            // 每次出牌后刷新所有手牌数值（如敌人脆弱等状态变化需即时反映）
            RefreshHandCardVisuals();
        }

        // =========================================================================
        // 事件回调：战斗结束
        // =========================================================================
        private void OnBattleEnded(BattleOutcome outcome)
        {
        }

        // =========================================================================
        // 事件回调：警告
        // =========================================================================
        private void OnStateWarning(string msg)
        {
            Debug.LogWarning($"[BattleHandDisplay] 状态警告：{msg}");
        }

        // =========================================================================
        // 事件回调：日志
        // =========================================================================
        private void OnCombatLog(string msg)
        {
            // 默认只打 Debug，如果你有屏幕内日志 UI 可以在这里追加
            Debug.Log(msg);
        }

        // =========================================================================
        // 按钮回调
        // =========================================================================

        /// <summary>
        /// 手牌被点击 → 调用战斗控制器出牌。
        /// </summary>
        private void OnCardClicked(CardInfo card)
        {
            if (battleController == null || card == null) return;

            // 特效播放期间或动画播放期间禁用卡牌交互
            if (effectController != null && effectController.IsPlayingEffect)
                return;

            if (IsAnimating)
                return;

            if (!battleController.CanPlayCard(card))
            {
                Debug.Log($"[BattleHandDisplay] 无法打出 {card.name}（能量不足或不在手牌中）");
                return;
            }

            bool success = battleController.PlayCard(card);
            if (success)
            {
                Debug.Log($"[BattleHandDisplay] 成功打出：{card.name}");
            }
        }

        /// <summary>
        /// 结束回合按钮被点击。
        /// </summary>
        private void OnEndTurnClicked()
        {
            if (battleController != null)
            {
                if (IsAnimating) return;

                bool effectLocked = effectController != null && effectController.IsPlayingEffect;
                if (effectLocked) return;

                battleController.EndPlayerTurn();
            }
        }

        // =========================================================================
        // 辅助函数
        // =========================================================================

        /// <summary>
        /// 根据当前能量和阶段，以及抽弃牌动画状态，刷新所有手牌按钮的可交互状态。
        /// </summary>
        private void RefreshHandInteractable()
        {
            bool effectLocked = effectController != null && effectController.IsPlayingEffect;
            bool animating = IsAnimating;

            bool baseCanPlay = !effectLocked && !animating
                               && battleController.Phase == BattleTurnPhase.PlayerTurn;

            foreach (var go in handObjects)
            {
                if (go == null) continue;
                Button btn = go.GetComponent<Button>();
                if (btn == null) continue;

                CardUI cardUI = go.GetComponent<CardUI>();
                if (cardUI == null) continue;

                var dragHandler = go.GetComponent<CardDragHandler>();
                CardInfo card = dragHandler != null ? dragHandler.CardInfo : null;

                bool canPlay = baseCanPlay && card != null && battleController.CanPlayCard(card);
                btn.interactable = canPlay;
            }
        }

        private void RefreshHandCardVisualsWithGlucose(float glucose)
        {
            RefreshHandCardVisuals();
        }

        public void RefreshHandCardVisuals()
        {
            if (battleController == null || battleController.PlayerStats == null) return;

            foreach (var go in handObjects)
            {
                if (go == null) continue;

                var cardUI = go.GetComponent<CardUI>();
                var dragHandler = go.GetComponent<CardDragHandler>();
                if (cardUI != null && dragHandler != null && dragHandler.CardInfo != null)
                {
                    SetCardRealTimeValues(cardUI, dragHandler.CardInfo);
                }
            }
        }

        /// <summary>
        /// 根据玩家和敌人的实时状态计算卡牌实际数值并渲染到 UI。
        /// </summary>
        private void SetCardRealTimeValues(CardUI cardUI, CGM.Data.CardInfo card)
        {
            if (battleController == null || card == null) return;
            var player = battleController.PlayerStats;
            var enemy = battleController.EnemyStats;

            // 对敌人的实际伤害（含敌人脆弱）
            int actualDamage = enemy != null
                ? CGM.Core.BattleCalculator.CalculateDamage(card, player, enemy)
                : CGM.Core.BattleCalculator.CalculateSelfDamage(card, player);

            // 自身格挡（不受敌人影响）
            int actualBlock = CGM.Core.BattleCalculator.CalculateSelfBlock(card, player);

            float glucoseMultiplier = player != null
                ? CGM.Core.BattleCalculator.GetGlucoseChangeMultiplier(player) : 1.0f;

            cardUI.SetCard(card,
                actualDamage - card.finalDamage,
                actualBlock - card.finalBlock,
                glucoseMultiplier);
        }

        private void OnCardOverflowed(CardInfo card)
        {
            overflowQueue.Enqueue(card);
            if (!isProcessingOverflow)
            {
                StartCoroutine(ProcessOverflowQueueRoutine());
            }
        }

        private IEnumerator ProcessOverflowQueueRoutine()
        {
            isProcessingOverflow = true;

            while (overflowQueue.Count > 0)
            {
                CardInfo card = overflowQueue.Dequeue();
                activeOverflowAnimations++;
                StartCoroutine(OverflowSingleCardRoutine(card, () => {
                    activeOverflowAnimations--;
                }));
                // 每次开始爆牌动画后，等待 0.15 秒再处理下一张，实现优雅的瀑布流效果
                yield return new WaitForSeconds(0.15f);
            }

            while (activeOverflowAnimations > 0)
            {
                yield return null;
            }

            isProcessingOverflow = false;
            CheckAndSyncCounters();
        }

        private IEnumerator OverflowSingleCardRoutine(CardInfo card, System.Action onComplete)
        {
            // 1. 起点：抽牌堆位置
            Vector2 startPos = drawPileTarget != null
                ? (Vector2)drawPileTarget.position
                : new Vector2(Screen.width * 0.5f, -300f);

            // 2. 终点：手牌区最右侧偏移位置（模拟飞到手里）
            Vector2 endPos = handContainer.position;
            if (handObjects.Count > 0 && handObjects[handObjects.Count - 1] != null)
            {
                endPos = handObjects[handObjects.Count - 1].GetComponent<RectTransform>().position + new Vector3(140f, 0f, 0f);
            }

            // 3. 实例化临时卡牌
            GameObject cardGo = Instantiate(cardPrefab, handContainer);
            cardGo.name = $"OverflowCard_{card.id}";

            var cardUI = cardGo.GetComponent<CardUI>();
            if (cardUI != null)
            {
                SetCardRealTimeValues(cardUI, card);
            }

            // 溢出卡牌不可交互和拖拽，直接清理其触发器组件
            var dragHandler = cardGo.GetComponent<CardDragHandler>();
            if (dragHandler != null) Destroy(dragHandler);
            var btn = cardGo.GetComponent<Button>();
            if (btn != null) Destroy(btn);

            // 脱离 LayoutGroup 移动到 Canvas 根节点下播放飞行动画
            var canvas = GetComponentInParent<Canvas>()?.rootCanvas;
            var canvasRoot = canvas != null ? canvas.transform : transform.parent;
            var cg = cardGo.GetComponent<CanvasGroup>();
            if (cg == null) cg = cardGo.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            cardGo.transform.SetParent(canvasRoot, true);

            var anim = cardGo.GetComponent<CardAnimator>();
            if (anim == null) anim = cardGo.AddComponent<CardAnimator>();

            // 4. 播放从抽牌堆飞入的摸牌动画
            bool drawDone = false;
            anim.PlayDrawAnimation(startPos, endPos, null, () => {
                drawDone = true;
            });

            while (!drawDone) yield return null;

            // 5. 稍微停留 0.25 秒供看清被爆掉的是什么牌
            yield return new WaitForSeconds(0.25f);

            // 6. 播放飞往弃牌堆的动画并自动销毁
            if (discardPileTarget != null)
            {
                activeDiscardAnimations++;
                bool discardDone = false;
                anim.PlayDiscardAnimation(discardPileTarget.position, () => {
                    activeDiscardAnimations--;
                    discardDone = true;
                    // 弃牌飞完时，视觉计数+1
                    if (visualDiscardCount < targetDiscardCount)
                    {
                        visualDiscardCount++;
                        UpdateCountTexts();
                    }
                    CheckAndSyncCounters();
                });

                while (!discardDone) yield return null;
            }
            else
            {
                Destroy(cardGo);
            }

            onComplete?.Invoke();
        }
    }
}
