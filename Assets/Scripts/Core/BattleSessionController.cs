using System;
using System.Collections.Generic;
using UnityEngine;
using CGM.Data;

namespace CGM.Core
{
    /// <summary>
    /// 单局战斗的总控入口，负责回合推进、出牌、敌人行动与胜负判定。
    /// </summary>
    public enum BattleTurnPhase
    {
        NotStarted,
        PlayerTurn,
        EnemyTurn,
        Victory,
        Defeat
    }

    /// <summary>
    /// 战斗结束结果。
    /// </summary>
    public enum BattleOutcome
    {
        Victory,
        Defeat,
        Cancelled
    }

    public class BattleSessionController : MonoBehaviour
    {
        [Header("参与者引用")]
        [SerializeField] private PlayerStats playerStats;
        [SerializeField] private EnemyStats enemyStats;
        [SerializeField] private CardDatabase cardDatabase;
        [SerializeField] private EnemyDatabase enemyDatabase;

        [Header("起始配置")]
        [SerializeField] private string startingEnemyId;
        [SerializeField] private bool resetEnemyOnBattleStart = true;
        [SerializeField] private bool autoStartBattle = false; // 由 GameSessionManager 统一管理战斗启动

        [Header("回合配置")]
        [SerializeField] private int startingEnergy = 3;
        [SerializeField] private int cardsPerTurn = 5;
        [SerializeField] private int maximumHandSize = 10;

        [Header("起始牌组")]
        [SerializeField] private List<string> startingDeckCardIds = new List<string>
        {
            "starter_rice",
            "starter_rice",
            "starter_rice",
            "starter_rice",
            "starter_rice",
            "starter_walk",
            "starter_walk",
            "starter_walk",
            "starter_walk",
            "starter_walk"
        };

        [Header("战斗奖励")]
        [SerializeField] private int rewardCardCount = 3;

        [Header("调试配置")]
        [Tooltip("调试模式下按 A 键往抽牌堆中添加的卡牌 ID")]
        [SerializeField] private string debugAddCardId = "starter_rice";

        private readonly BattleCardPile cardPile = new BattleCardPile();
        private readonly List<CardInfo> pendingRewardCards = new List<CardInfo>();

        public event Action<BattleTurnPhase> OnPhaseChanged;
        public event Action<int, int> OnEnergyChanged;
        public event Action<IReadOnlyList<CardInfo>> OnHandChanged;
        public event Action<IReadOnlyList<CardInfo>, IReadOnlyList<CardInfo>, IReadOnlyList<CardInfo>> OnPilesChanged;
        public event Action<CardPlayResult> OnCardPlayed;
        public event Action<EnemyIntentInfo> OnEnemyIntentResolved;
        public event Action OnEnemyAttackFullyBlocked;
        public event Action<IReadOnlyList<CardInfo>> OnRewardsGenerated;
        public event Action<BattleOutcome> OnBattleEnded;
        public event Action<string> OnCombatLog;
        public event Action<string> OnStateWarning;
        public event Action<CardInfo> OnCardOverflowed;

        public PlayerStats PlayerStats => playerStats;
        public EnemyStats EnemyStats => enemyStats;

        public BattleTurnPhase Phase { get; private set; } = BattleTurnPhase.NotStarted;
        public int CurrentEnergy { get; private set; }
        public int MaxEnergy => startingEnergy;
        public int TurnNumber { get; private set; }
        public string DefeatReason { get; private set; } = "";
        public IReadOnlyList<CardInfo> Hand => cardPile.Hand;
        public IReadOnlyList<CardInfo> DrawPile => cardPile.DrawPile;
        public IReadOnlyList<CardInfo> DiscardPile => cardPile.DiscardPile;
        public IReadOnlyList<CardInfo> PendingRewardCards => pendingRewardCards;
        public List<string> StartingDeckCardIds => startingDeckCardIds;
        public string StartingEnemyId
        {
            get => startingEnemyId;
            set => startingEnemyId = value;
        }

        public void AddCardToStartingDeck(string cardId)
        {
            if (startingDeckCardIds == null)
            {
                startingDeckCardIds = new List<string>();
            }
            startingDeckCardIds.Add(cardId);
        }

        /// <summary>
        /// 重置牌组到默认初始状态（新游戏开始时调用）。
        /// </summary>
        public void ResetDeckToDefault(List<string> defaultDeck)
        {
            startingDeckCardIds = new List<string>(defaultDeck);
        }

        private bool battleEnded;

        private void Awake()
        {
            // 强制禁用自动启动，由 GameSessionManager 统一管理战斗生命周期
            autoStartBattle = false;
            ResolveDependencies();
        }

        private void Start()
        {
            if (autoStartBattle)
            {
                StartBattle();
            }
        }

#if UNITY_EDITOR
        private void Update()
        {
            if (Phase == BattleTurnPhase.PlayerTurn)
            {
                // 按 A 键手动在抽牌堆加牌
                if (Input.GetKeyDown(KeyCode.A))
                {
                    AddDummyCardToDrawPile();
                    NotifyPilesChanged();
                }
                // 按 D 键抽 1 张牌
                if (Input.GetKeyDown(KeyCode.D))
                {
                    DrawCards(1);
                    Debug.Log("[Debug] 按 D 键强行抽牌 1 张。");
                }
                // 按 J 键抽 2 张牌
                if (Input.GetKeyDown(KeyCode.J))
                {
                    DrawCards(2);
                    Debug.Log("[Debug] 按 J 键强行抽牌 2 张。");
                }
                // 按 K 键抽 3 张牌
                if (Input.GetKeyDown(KeyCode.K))
                {
                    DrawCards(3);
                    Debug.Log("[Debug] 按 K 键强行抽牌 3 张。");
                }
                // 按 F 键直接填满并溢出手牌 (填满并额外溢出 2 张)
                if (Input.GetKeyDown(KeyCode.F))
                {
                    int needed = maximumHandSize - Hand.Count + 2;
                    if (needed > 0)
                    {
                        DrawCards(needed);
                        Debug.Log($"[Debug] 按 F 键强行抽满并溢出。需抽数: {needed}");
                    }
                }
                // 按向上键强行增加血糖 1.0 
                if (Input.GetKeyDown(KeyCode.UpArrow) && playerStats != null)
                {
                    playerStats.SetGlucose(playerStats.CurrentGlucose + 1.0f);
                    Debug.Log($"[Debug] 按向上方向键强行增加当前血糖 1.0。当前血糖: {playerStats.CurrentGlucose:F1}");
                    CheckBattleEnd();
                }
                // 按向下键强行降低血糖 1.0
                if (Input.GetKeyDown(KeyCode.DownArrow) && playerStats != null)
                {
                    playerStats.SetGlucose(playerStats.CurrentGlucose - 1.0f);
                    Debug.Log($"[Debug] 按向下方向键强行降低当前血糖 1.0。当前血糖: {playerStats.CurrentGlucose:F1}");
                    CheckBattleEnd();
                }
            }
        }

        private void AddDummyCardToDrawPile()
        {
            if (cardDatabase == null) return;
            string targetId = string.IsNullOrEmpty(debugAddCardId) ? "starter_rice" : debugAddCardId;
            CardInfo card = cardDatabase.GetCardById(targetId);
            if (card != null)
            {
                cardPile.AddToDrawPile(card);
                Debug.Log($"[Debug] 成功添加卡牌「{card.name}」(ID: {targetId}) 到抽牌堆。");
            }
            else
            {
                Debug.LogError($"[Debug] 未找到卡牌 ID: {targetId}");
            }
        }
#endif

        /// <summary>
        /// 彻底清空/重置战斗会话的数据状态（当退出战斗或返回主菜单时调用，以防数据留存）。
        /// </summary>
        public void ResetBattleSession()
        {
            Phase = BattleTurnPhase.NotStarted;
            battleEnded = true;
            DefeatReason = "";
            TurnNumber = 0;
            CurrentEnergy = 0;
            pendingRewardCards.Clear();
            cardPile.Clear(); // 彻底清空手牌、抽牌堆、弃牌堆
            
            // 广播空状态，通知 UI 刷新
            NotifyPhaseChanged();
            NotifyEnergyChanged();
            NotifyPilesChanged();
            OnHandChanged?.Invoke(new List<CardInfo>());
        }

        /// <summary>
        /// 立即开始一场战斗。
        /// </summary>
        public void StartBattle(IEnumerable<string> deckCardIds = null)
        {
            ResolveDependencies();

            if (playerStats == null)
            {
                LogCombat("[BattleSession] 缺少 PlayerStats，无法开始战斗。");
                return;
            }

            if (enemyStats == null)
            {
                LogCombat("[BattleSession] 缺少 EnemyStats，无法开始战斗。");
                return;
            }

            if (resetEnemyOnBattleStart)
            {
                ResetEnemyStateIfPossible();
            }

            List<CardInfo> startingDeck = BuildStartingDeck(deckCardIds ?? startingDeckCardIds);
            if (startingDeck.Count == 0)
            {
                LogCombat("[BattleSession] 起始牌组为空，无法开始战斗。");
                return;
            }

            pendingRewardCards.Clear();
            cardPile.Reset(startingDeck);

            battleEnded = false;
            DefeatReason = "";
            TurnNumber = 1;
            CurrentEnergy = startingEnergy;
            Phase = BattleTurnPhase.PlayerTurn;

            // 新战斗开始时清空双方状态栏（格挡 + 所有 Buff/Debuff）
            playerStats.ClearBlock();
            playerStats.ClearAllBuffs();
            enemyStats.ClearBlock();
            enemyStats.ClearAllBuffs();

            NotifyPhaseChanged();
            NotifyEnergyChanged();
            NotifyPilesChanged();

            DrawCards(cardsPerTurn);
            LogCombat($"[BattleSession] 战斗开始，对手：{GetEnemyName()}。");
        }

        /// <summary>
        /// 当前玩家是否能够出牌。
        /// </summary>
        public bool CanPlayCard(CardInfo card)
        {
            return !battleEnded
                && Phase == BattleTurnPhase.PlayerTurn
                && card != null
                && cardPile.ContainsInHand(card)
                && card.energyCost <= CurrentEnergy;
        }

        /// <summary>
        /// 打出一张手牌，默认攻击当前敌人。
        /// </summary>
        public bool PlayCard(CardInfo card, EntityStats primaryTarget = null)
        {
            if (!CanPlayCard(card))
            {
                return false;
            }

            // 特效前锁定视觉 UI 属性，待特效队列处理完时再放开
            var pUI = FindObjectOfType<UI.PlayerUI>(true);
            var eUI = FindObjectOfType<UI.EnemyUI>(true);
            if (pUI != null) pUI.HoldVisualStats = true;
            if (eUI != null) eUI.HoldVisualStats = true;

            CurrentEnergy -= card.energyCost;
            cardPile.RemoveFromHand(card); // 仅从手牌移出，先不送入弃牌堆，防止抽牌洗回自己

            EntityStats target = primaryTarget != null ? primaryTarget : enemyStats;
            CardPlayResult result = BattleCardEffectResolver.Resolve(card, playerStats, target, DrawCards, GainEnergy);

            // 效果完全结算完毕后，将本张卡送入弃牌堆
            cardPile.AddToDiscardPile(card);

            OnCardPlayed?.Invoke(result);
            NotifyEnergyChanged();
            NotifyHandChanged();
            NotifyPilesChanged();

            LogCombat($"[BattleSession] 打出 {card.name}，伤害 {result.DamageDealt}，格挡 {result.BlockGained}，抽牌 {result.CardsDrawn}，自损 {result.SelfDamage}，血糖变化 {result.GlucoseDelta:F1}。");

            CheckBattleEnd();
            return true;
        }

        /// <summary>
        /// 增加当前玩家的能量值。
        /// </summary>
        public void GainEnergy(int amount)
        {
            if (amount <= 0) return;
            CurrentEnergy += amount;
            NotifyEnergyChanged();
            LogCombat($"[BattleSession] 获得 {amount} 点能量，当前能量：{CurrentEnergy}。");
        }

        /// <summary>
        /// 按卡牌 ID 打出手牌中的第一张匹配卡。
        /// </summary>
        public bool PlayCard(string cardId, EntityStats primaryTarget = null)
        {
            if (string.IsNullOrEmpty(cardId))
            {
                return false;
            }

            foreach (var card in cardPile.Hand)
            {
                if (card != null && card.id == cardId)
                {
                    return PlayCard(card, primaryTarget);
                }
            }

            return false;
        }

        /// <summary>
        /// 结束玩家回合，驱动敌人执行当前意图。
        /// </summary>
        public void EndPlayerTurn()
        {
            if (battleEnded || Phase != BattleTurnPhase.PlayerTurn)
            {
                return;
            }

            cardPile.DiscardHand();
            NotifyHandChanged();
            NotifyPilesChanged();

            // 玩家身上 Buff 每回合结束时衰减
            if (playerStats != null)
            {
                playerStats.TickBuffsEndOfTurn();
            }

            // 敌方格挡在玩家回合结束时清空
            enemyStats.ClearBlock();

            Phase = BattleTurnPhase.EnemyTurn;
            NotifyPhaseChanged();

            ResolveEnemyTurn();
        }

        /// <summary>
        /// 直接终止当前战斗。
        /// </summary>
        public void CancelBattle()
        {
            FinishBattle(BattleOutcome.Cancelled);
        }

        private void ResolveEnemyTurn()
        {
            if (battleEnded || enemyStats == null || playerStats == null)
            {
                return;
            }

            EnemyIntentInfo intent = enemyStats.GetCurrentIntent();
            if (intent != null)
            {
                LogCombat($"[BattleSession] 敌人行动：{intent.actionType}。");

                // 特效前锁定视觉 UI 属性，待特效队列处理完时再放开
                var pUI = FindObjectOfType<UI.PlayerUI>(true);
                var eUI = FindObjectOfType<UI.EnemyUI>(true);
                if (pUI != null) pUI.HoldVisualStats = true;
                if (eUI != null) eUI.HoldVisualStats = true;

                // 记录玩家战前状态，用于判定攻击是否被完全格挡
                int preHp = playerStats.CurrentHp;
                enemyStats.ExecuteIntent(playerStats);

                // 攻击被玩家全格挡时，通知特效系统播放 Defend 而非 Attack
                bool playerFullyBlocked = intent.actionType == "attack"
                    && playerStats.CurrentHp == preHp
                    && intent.GetValue() > 0;

                if (playerFullyBlocked)
                    OnEnemyAttackFullyBlocked?.Invoke();
                else
                    OnEnemyIntentResolved?.Invoke(intent);
            }

            enemyStats.TickBuffsEndOfTurn();
            // enemyStats.IncrementTurnScaling(); // 已取消回合递增机制，防止中后期数值过于变态
            playerStats.ClearBlock();

            if (CheckBattleEnd())
            {
                return;
            }

            BeginNextPlayerTurn();
        }

        private void BeginNextPlayerTurn()
        {
            if (battleEnded)
            {
                return;
            }

            TurnNumber++;
            Phase = BattleTurnPhase.PlayerTurn;
            CurrentEnergy = startingEnergy;

            NotifyPhaseChanged();
            NotifyEnergyChanged();

            DrawCards(cardsPerTurn);
            LogCombat($"[BattleSession] 进入第 {TurnNumber} 回合。");
        }

        private int DrawCards(int count)
        {
            if (count <= 0)
            {
                return 0;
            }

            List<CardInfo> drawnCards = cardPile.Draw(count, maximumHandSize, (overflowCard) => {
                OnCardOverflowed?.Invoke(overflowCard);
            });

            if (drawnCards.Count > 0)
            {
                NotifyHandChanged();
                NotifyPilesChanged();
            }
            else
            {
                NotifyPilesChanged();
            }

            return drawnCards.Count;
        }

        private bool CheckBattleEnd()
        {
            if (battleEnded)
            {
                return true;
            }

            // 血糖低于 2.0 或高于 15.0 直接判负
            if (playerStats != null)
            {
                float glucose = Mathf.Round(playerStats.CurrentGlucose * 10f) / 10f;
                if (glucose <= BattleConstants.GlucoseDeathMin)
                {
                    DefeatReason = "血糖过低";
                    LogCombat($"[BattleSession] 血糖过低 {glucose:F1} <= {BattleConstants.GlucoseDeathMin} —— 血糖缺失！");
                    OnStateWarning?.Invoke("血糖过低，生命垂危！");
                    FinishBattle(BattleOutcome.Defeat);
                    return true;
                }
                if (glucose >= BattleConstants.GlucoseDeathMax)
                {
                    DefeatReason = "血糖过高";
                    LogCombat($"[BattleSession] 血糖过高 {glucose:F1} >= {BattleConstants.GlucoseDeathMax} —— 高血糖危象！");
                    OnStateWarning?.Invoke("血糖过高，高血糖危象！");
                    FinishBattle(BattleOutcome.Defeat);
                    return true;
                }
            }

            if (playerStats != null && playerStats.IsDead)
            {
                DefeatReason = "血量过低";
                FinishBattle(BattleOutcome.Defeat);
                return true;
            }

            if (enemyStats != null && enemyStats.IsDead)
            {
                GenerateRewards();
                FinishBattle(BattleOutcome.Victory);
                return true;
            }

            return false;
        }

        private void FinishBattle(BattleOutcome outcome)
        {
            if (battleEnded)
            {
                return;
            }

            battleEnded = true;
            Phase = outcome == BattleOutcome.Victory
                ? BattleTurnPhase.Victory
                : outcome == BattleOutcome.Defeat
                    ? BattleTurnPhase.Defeat
                    : BattleTurnPhase.NotStarted;

            NotifyPhaseChanged();
            OnBattleEnded?.Invoke(outcome);
            LogCombat($"[BattleSession] 战斗结束：{outcome}。");
        }

        private void GenerateRewards()
        {
            pendingRewardCards.Clear();
            pendingRewardCards.AddRange(RandomManager.GetRandomRewardCardsForCurrentLevel(rewardCardCount));
            OnRewardsGenerated?.Invoke(pendingRewardCards);
        }

        private void ResolveDependencies()
        {
            if (playerStats == null)
            {
                playerStats = FindObjectOfType<PlayerStats>();
            }

            if (enemyStats == null)
            {
                enemyStats = FindObjectOfType<EnemyStats>();
            }

            if (cardDatabase == null)
            {
                cardDatabase = CardDatabase.Instance != null
                    ? CardDatabase.Instance
                    : FindObjectOfType<CardDatabase>();

                if (cardDatabase == null)
                {
                    GameObject databaseGo = new GameObject("[Runtime_CardDatabase]");
                    cardDatabase = databaseGo.AddComponent<CardDatabase>();
                }
            }

            if (enemyDatabase == null)
            {
                enemyDatabase = EnemyDatabase.Instance != null
                    ? EnemyDatabase.Instance
                    : FindObjectOfType<EnemyDatabase>();

                if (enemyDatabase == null)
                {
                    GameObject databaseGo = new GameObject("[Runtime_EnemyDatabase]");
                    enemyDatabase = databaseGo.AddComponent<EnemyDatabase>();
                }
            }
        }

        private void ResetEnemyStateIfPossible()
        {
            if (enemyStats == null)
            {
                return;
            }

            // 优先使用面板配置的 startingEnemyId（自动 Trim 空格）
            if (!string.IsNullOrEmpty(startingEnemyId))
            {
                enemyStats.LoadEnemy(startingEnemyId.Trim());
                return;
            }

            // 回退到敌人身上已有的 Id 配置
            string existingId = enemyStats.EnemyId ?? "";
            if (!string.IsNullOrEmpty(existingId))
            {
                enemyStats.LoadEnemy(existingId.Trim());
                return;
            }

            // 最后只清格挡
            enemyStats.ClearBlock();
        }

        private List<CardInfo> BuildStartingDeck(IEnumerable<string> deckCardIds)
        {
            List<CardInfo> deck = new List<CardInfo>();
            if (deckCardIds == null)
            {
                return deck;
            }

            foreach (var cardId in deckCardIds)
            {
                if (string.IsNullOrEmpty(cardId))
                {
                    continue;
                }

                CardInfo card = cardDatabase != null ? cardDatabase.GetCardById(cardId) : null;
                if (card == null)
                {
                    LogCombat($"[BattleSession] 未找到卡牌 ID：{cardId}。");
                    continue;
                }

                deck.Add(card);
            }

            return deck;
        }

        private string GetEnemyName()
        {
            if (enemyStats != null && enemyStats.EnemyInfo != null)
            {
                return enemyStats.EnemyInfo.name;
            }

            return string.IsNullOrEmpty(startingEnemyId) ? "Unknown Enemy" : startingEnemyId;
        }

        private void NotifyPhaseChanged()
        {
            OnPhaseChanged?.Invoke(Phase);
        }

        private void NotifyEnergyChanged()
        {
            OnEnergyChanged?.Invoke(CurrentEnergy, startingEnergy);
        }

        private void NotifyHandChanged()
        {
            OnHandChanged?.Invoke(cardPile.Hand);
        }

        private void NotifyPilesChanged()
        {
            OnPilesChanged?.Invoke(cardPile.Hand, cardPile.DrawPile, cardPile.DiscardPile);
        }

        private void LogCombat(string message)
        {
            Debug.Log(message);
            OnCombatLog?.Invoke(message);
        }
    }
}