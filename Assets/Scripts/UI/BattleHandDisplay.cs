// =============================================================================
// BattleHandDisplay.cs — 手牌 UI 桥接器
// 命名空间：CGM.UI
// 职责：订阅 BattleSessionController 事件，自动刷新卡牌、能量、回合状态等 UI。
//       同时为每张手牌挂载点击出牌回调。
// =============================================================================

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
        [Tooltip("手牌卡牌的父节点（如 HandPanel）")]
        [SerializeField] private Transform handContainer;
        [Tooltip("单张卡牌的预制体（必须挂有 CardUI 组件）")]
        [SerializeField] private GameObject cardPrefab;

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

        // 当前手牌对象池
        private readonly List<GameObject> handObjects = new List<GameObject>();

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

            // 战斗日志 → Debug 输出
            battleController.OnCombatLog += OnCombatLog;

            // =====================================================================
            // 按钮绑定
            // =====================================================================
            if (endTurnButton != null)
            {
                endTurnButton.onClick.AddListener(OnEndTurnClicked);
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
                battleController.OnCombatLog -= OnCombatLog;
            }
        }

        // =========================================================================
        // 事件回调：手牌变化
        // =========================================================================
        private void OnHandChanged(IReadOnlyList<CardInfo> handCards)
        {
            // 1. 销毁旧的手牌对象
            foreach (var go in handObjects)
            {
                Destroy(go);
            }
            handObjects.Clear();

            if (handCards == null || handContainer == null || cardPrefab == null)
            {
                return;
            }

            // 2. 为每张卡牌实例化 UI，并绑定点击事件
            foreach (var card in handCards)
            {
                GameObject cardGo = Instantiate(cardPrefab, handContainer);
                cardGo.name = $"HandCard_{card.id}";

                CardUI cardUI = cardGo.GetComponent<CardUI>();
                if (cardUI != null)
                {
                    // 计算自身状态修正后的卡面预览值
                    var player = FindObjectOfType<CGM.Core.PlayerStats>();
                    int projectedDmg = CGM.Core.BattleCalculator.CalculateSelfDamage(card, player);
                    int projectedBlk = CGM.Core.BattleCalculator.CalculateSelfBlock(card, player);
                    int dmgMod = projectedDmg - card.finalDamage;
                    int blkMod = projectedBlk - card.finalBlock;
                    cardUI.SetCard(card, dmgMod, blkMod);
                }

                // 设置拖拽处理器
                var dragHandler = cardGo.GetComponent<CardDragHandler>();
                if (dragHandler == null)
                    dragHandler = cardGo.AddComponent<CardDragHandler>();
                dragHandler.SetCardInfo(card);

                // 挂载点击出牌回调（保留点击作为快速出牌方式）
                Button btn = cardGo.GetComponent<Button>();
                if (btn == null)
                {
                    btn = cardGo.AddComponent<Button>();
                }
                btn.onClick.RemoveAllListeners();

                // 用局部变量捕获当前卡牌（闭包陷阱防护）
                CardInfo capturedCard = card;
                btn.onClick.AddListener(() => OnCardClicked(capturedCard));

                // 根据能否出牌设置交互状态
                bool canPlay = battleController.CanPlayCard(card);
                btn.interactable = canPlay;

                handObjects.Add(cardGo);
            }
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

            // 只有玩家回合才能点结束回合
            bool isPlayerTurn = phase == BattleTurnPhase.PlayerTurn;
            if (endTurnButton != null)
            {
                endTurnButton.interactable = isPlayerTurn;
            }

            RefreshHandInteractable();
        }

        // =========================================================================
        // 事件回调：牌堆计数
        // =========================================================================
        private void OnPilesChanged(IReadOnlyList<CardInfo> hand, IReadOnlyList<CardInfo> draw, IReadOnlyList<CardInfo> discard)
        {
            if (drawPileCountText != null)
            {
                drawPileCountText.text = (draw?.Count ?? 0).ToString();
            }
            if (discardPileCountText != null)
            {
                discardPileCountText.text = (discard?.Count ?? 0).ToString();
            }
        }

        // =========================================================================
        // 事件回调：出牌完成
        // =========================================================================
        private void OnCardPlayed(CardPlayResult result)
        {
            Debug.Log($"[BattleHandDisplay] 出牌结算完成：{result.Card?.name}，" +
                      $"伤害 {result.DamageDealt}，格挡 {result.BlockGained}，" +
                      $"抽牌 {result.CardsDrawn}，血糖变化 {result.GlucoseDelta:F1}");
        }

        // =========================================================================
        // 事件回调：战斗结束
        // =========================================================================
        private void OnBattleEnded(BattleOutcome outcome)
        {
            Debug.Log($"[BattleHandDisplay] 战斗结束：{outcome}");
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
                battleController.EndPlayerTurn();
            }
        }

        // =========================================================================
        // 辅助函数
        // =========================================================================

        /// <summary>
        /// 根据当前能量和阶段，刷新所有手牌按钮的可交互状态。
        /// </summary>
        private void RefreshHandInteractable()
        {
            foreach (var go in handObjects)
            {
                Button btn = go.GetComponent<Button>();
                if (btn == null) continue;

                CardUI cardUI = go.GetComponent<CardUI>();
                if (cardUI == null) continue;

                // 用反射获取当前卡片数据不方便，直接查 battleController
                bool canPlay = battleController.Phase == BattleTurnPhase.PlayerTurn
                               && battleController.CurrentEnergy > 0;
                btn.interactable = canPlay;
            }
        }
    }
}
