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
        [SerializeField] private bool autoStartBattle = true;

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

        private readonly BattleCardPile cardPile = new BattleCardPile();
        private readonly List<CardInfo> pendingRewardCards = new List<CardInfo>();

        public event Action<BattleTurnPhase> OnPhaseChanged;
        public event Action<int, int> OnEnergyChanged;
        public event Action<IReadOnlyList<CardInfo>> OnHandChanged;
        public event Action<IReadOnlyList<CardInfo>, IReadOnlyList<CardInfo>, IReadOnlyList<CardInfo>> OnPilesChanged;
        public event Action<CardPlayResult> OnCardPlayed;
        public event Action<EnemyIntentInfo> OnEnemyIntentResolved;
        public event Action<IReadOnlyList<CardInfo>> OnRewardsGenerated;
        public event Action<BattleOutcome> OnBattleEnded;
        public event Action<string> OnCombatLog;
        public event Action<string> OnStateWarning;

        public BattleTurnPhase Phase { get; private set; } = BattleTurnPhase.NotStarted;
        public int CurrentEnergy { get; private set; }
        public int MaxEnergy => startingEnergy;
        public int TurnNumber { get; private set; }
        public IReadOnlyList<CardInfo> Hand => cardPile.Hand;
        public IReadOnlyList<CardInfo> DrawPile => cardPile.DrawPile;
        public IReadOnlyList<CardInfo> DiscardPile => cardPile.DiscardPile;
        public IReadOnlyList<CardInfo> PendingRewardCards => pendingRewardCards;

        private bool battleEnded;

        private void Awake()
        {
            ResolveDependencies();
        }

        private void Start()
        {
            if (autoStartBattle)
            {
                StartBattle();
            }
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
            TurnNumber = 1;
            CurrentEnergy = startingEnergy;
            Phase = BattleTurnPhase.PlayerTurn;

            playerStats.ClearBlock();
            enemyStats.ClearBlock();

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

            CurrentEnergy -= card.energyCost;
            cardPile.DiscardCard(card);

            EntityStats target = primaryTarget != null ? primaryTarget : enemyStats;
            CardPlayResult result = BattleCardEffectResolver.Resolve(card, playerStats, target, DrawCards);

            OnCardPlayed?.Invoke(result);
            NotifyEnergyChanged();
            NotifyHandChanged();
            NotifyPilesChanged();

            LogCombat($"[BattleSession] 打出 {card.name}，伤害 {result.DamageDealt}，格挡 {result.BlockGained}，抽牌 {result.CardsDrawn}，自损 {result.SelfDamage}，血糖变化 {result.GlucoseDelta:F1}。");

            CheckBattleEnd();
            return true;
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
                OnEnemyIntentResolved?.Invoke(intent);
                LogCombat($"[BattleSession] 敌人行动：{intent.actionType}。");
                enemyStats.ExecuteIntent(playerStats);
            }

            playerStats.TickBuffsEndOfTurn();
            enemyStats.TickBuffsEndOfTurn();
            enemyStats.IncrementTurnScaling();
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

            List<CardInfo> drawnCards = cardPile.Draw(count, maximumHandSize);
            if (drawnCards.Count > 0)
            {
                NotifyHandChanged();
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
                float glucose = playerStats.CurrentGlucose;
                if (glucose < BattleConstants.GlucoseDeathMin)
                {
                    LogCombat($"[BattleSession] 血糖过低 {glucose:F1} < {BattleConstants.GlucoseDeathMin} —— 血糖缺失！");
                    OnStateWarning?.Invoke("血糖过低，生命垂危！");
                    FinishBattle(BattleOutcome.Defeat);
                    return true;
                }
                if (glucose > BattleConstants.GlucoseDeathMax)
                {
                    LogCombat($"[BattleSession] 血糖过高 {glucose:F1} > {BattleConstants.GlucoseDeathMax} —— 高血糖危象！");
                    OnStateWarning?.Invoke("血糖过高，高血糖危象！");
                    FinishBattle(BattleOutcome.Defeat);
                    return true;
                }
            }

            if (playerStats != null && playerStats.IsDead)
            {
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
            pendingRewardCards.AddRange(RandomManager.GetRandomRewardCards(rewardCardCount));
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