// =============================================================================
// BattleEffectController.cs — 战斗特效控制器
// 命名空间：CGM.UI
// 职责：管理攻击/格挡/状态特效的播放（视效 + 音效），
//       特效期间禁用卡牌交互，支持玩家出牌和敌人行动双向触发。
//       挂载在 BattlePanel 上。
// =============================================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using CGM.Core;
using CGM.Data;

namespace CGM.UI
{
    /// <summary>
    /// 战斗特效控制器。播放攻击、格挡、状态三种视觉特效并配合音效。
    /// 敌人行动时特效左右翻转 180°。
    /// </summary>
    public class BattleEffectController : MonoBehaviour
    {
        [Header("特效 UI 引用")]
        [SerializeField] private RectTransform attackEffect;
        [SerializeField] private RectTransform defendEffect;
        [SerializeField] private RectTransform statusEffect;

        [Header("被击抖动目标")]
        [Tooltip("敌人怪物图标（玩家攻击时抖动）")]
        [SerializeField] private RectTransform enemyMonster;
        [Tooltip("玩家角色图标（敌人攻击时抖动）")]
        [SerializeField] private RectTransform playerAvatar;

        [Header("格挡容器（获得格挡时弹跳缩放）")]
        [SerializeField] private RectTransform playerBlockContainer;
        [SerializeField] private RectTransform enemyBlockContainer;

        [Header("音效")]
        [SerializeField] private AudioClip attackSound;
        [SerializeField] private AudioClip defendSound;       // 攻击被全格挡 (bong_001)
        [SerializeField] private AudioClip gainBlockSound;    // 获得格挡 (tap-a)
        [SerializeField] private AudioClip statusSound;

        [Header("结束回合")]
        [Tooltip("特效播放期间禁用结束回合按钮")]
        [SerializeField] private Button endTurnButton;

        [Header("动画参数")]
        [SerializeField] private float effectScaleDuration = 0.2f;   // 放大动画时长
        [SerializeField] private float effectHoldDuration = 0.3f;    // 停留时长
        [SerializeField] private float effectFadeDuration = 0.4f;    // 淡出时长
        [SerializeField] private float shakeIntensity = 8f;          // 抖动强度（像素）
        [SerializeField] private float shakeDuration = 0.15f;        // 抖动时长

        [Header("战斗中枢")]
        [SerializeField] private BattleSessionController battleController;

        private Queue<EffectRequest> effectQueue = new Queue<EffectRequest>();
        private bool isProcessing = false;

        /// <summary>
        /// 当前是否正在播放特效（外部可据此禁用卡牌交互）。
        /// </summary>
        public bool IsPlayingEffect => isProcessing || effectQueue.Count > 0;

        private void Awake()
        {
            AutoResolveReferences();
        }

        private void Start()
        {
            if (battleController == null)
                battleController = FindObjectOfType<BattleSessionController>();

            if (battleController != null)
            {
                battleController.OnCardPlayed += OnCardPlayed;
                battleController.OnEnemyIntentResolved += OnEnemyIntentResolved;
                battleController.OnEnemyAttackFullyBlocked += OnEnemyAttackFullyBlocked;
            }
        }

        private void OnDestroy()
        {
            if (battleController != null)
            {
                battleController.OnCardPlayed -= OnCardPlayed;
                battleController.OnEnemyIntentResolved -= OnEnemyIntentResolved;
                battleController.OnEnemyAttackFullyBlocked -= OnEnemyAttackFullyBlocked;
            }
        }

        // =====================================================================
        // 事件回调
        // =====================================================================

        private void OnCardPlayed(CardPlayResult result)
        {
            if (result == null) return;

            var requests = BuildPlayerEffectRequests(result);
            foreach (var req in requests)
            {
                effectQueue.Enqueue(req);
            }
            if (!isProcessing) StartCoroutine(ProcessQueue());
        }

        private void OnEnemyIntentResolved(EnemyIntentInfo intent)
        {
            if (intent == null) return;
            var req = BuildEnemyEffectRequest(intent);
            if (req.HasValue)
            {
                effectQueue.Enqueue(req.Value);
                if (!isProcessing) StartCoroutine(ProcessQueue());
            }
        }

        private void OnEnemyAttackFullyBlocked()
        {
            // 敌人攻击被玩家完全格挡 → 播放 Defend 特效（翻转 180°）
            effectQueue.Enqueue(new EffectRequest { type = EffectType.Defend, isEnemy = true });
            if (!isProcessing) StartCoroutine(ProcessQueue());
        }

        // =====================================================================
        // 效应判定
        // =====================================================================

        /// <summary>
        /// 根据玩家出牌结果判定需要播放的特效列表。
        /// 多段攻击视为多次独立判定。
        /// </summary>
        private List<EffectRequest> BuildPlayerEffectRequests(CardPlayResult result)
        {
            var list = new List<EffectRequest>();

            int currentHp = result.TargetStartHp;
            int currentBlock = result.TargetStartBlock;

            // 1. 攻击 / 全格挡 — 多段攻击按每击独立判定
            if (result.HitResults.Count > 0)
            {
                foreach (var hit in result.HitResults)
                {
                    int dmg = hit.rawDamage;
                    if (currentBlock > 0)
                    {
                        if (currentBlock >= dmg)
                        {
                            currentBlock -= dmg;
                            dmg = 0;
                        }
                        else
                        {
                            dmg -= currentBlock;
                            currentBlock = 0;
                        }
                    }
                    if (dmg > 0)
                    {
                        currentHp = Mathf.Max(0, currentHp - dmg);
                    }

                    var req = hit.blocked
                        ? EffectReq(EffectType.Defend, false)
                        : EffectReq(EffectType.Attack, false);
                    req.visualHpAfterHit = currentHp;
                    req.visualBlockAfterHit = currentBlock;
                    req.targetStats = result.PrimaryTarget;
                    list.Add(req);
                }
            }
            else if (result.Card != null && result.Card.finalDamage > 0)
            {
                // 兜底：非 Hit 效果的攻击（如未来扩展）
                int dmg = result.DamageDealt;
                if (currentBlock > 0)
                {
                    if (currentBlock >= dmg)
                    {
                        currentBlock -= dmg;
                        dmg = 0;
                    }
                    else
                    {
                        dmg -= currentBlock;
                        currentBlock = 0;
                    }
                }
                if (dmg > 0)
                {
                    currentHp = Mathf.Max(0, currentHp - dmg);
                }

                var req = result.FullyBlocked
                    ? EffectReq(EffectType.Defend, false)
                    : EffectReq(EffectType.Attack, false);
                req.visualHpAfterHit = currentHp;
                req.visualBlockAfterHit = currentBlock;
                req.targetStats = result.PrimaryTarget;
                list.Add(req);
            }

            // 2. 获得格挡
            if (result.BlockGained > 0)
            {
                var req = EffectReq(EffectType.GainBlock, false, result.BlockGained);
                if (battleController != null && battleController.PlayerStats != null)
                {
                    req.visualHpAfterHit = battleController.PlayerStats.CurrentHp;
                    req.visualBlockAfterHit = battleController.PlayerStats.Block;
                    req.targetStats = battleController.PlayerStats;
                }
                list.Add(req);
            }

            // 3. 状态（仅 Buff/Debuff）
            bool hasStatus = result.Card != null && result.Card.effects != null &&
                result.Card.effects.Exists(e =>
                    e.effectType == "apply_buff" || e.effectType == "apply_debuff");

            if (hasStatus)
            {
                var req = EffectReq(EffectType.Status, false);
                if (battleController != null && battleController.PlayerStats != null)
                {
                    req.visualHpAfterHit = battleController.PlayerStats.CurrentHp;
                    req.visualBlockAfterHit = battleController.PlayerStats.Block;
                    req.targetStats = battleController.PlayerStats;
                }
                list.Add(req);
            }

            return list;
        }

        /// <summary>
        /// 根据敌人行动意图判定特效。
        /// </summary>
        private EffectRequest? BuildEnemyEffectRequest(EnemyIntentInfo intent)
        {
            if (string.IsNullOrEmpty(intent.actionType)) return null;
            var req = new EffectRequest { isEnemy = true };
            switch (intent.actionType.ToLower())
            {
                case "attack":
                    req.type = EffectType.Attack;
                    if (battleController != null && battleController.PlayerStats != null)
                    {
                        req.visualHpAfterHit = battleController.PlayerStats.CurrentHp;
                        req.visualBlockAfterHit = battleController.PlayerStats.Block;
                        req.targetStats = battleController.PlayerStats;
                    }
                    break;
                case "block":
                    req.type = EffectType.GainBlock;
                    req.blockValue = intent.GetValue();
                    if (battleController != null && battleController.EnemyStats != null)
                    {
                        req.visualHpAfterHit = battleController.EnemyStats.CurrentHp;
                        req.visualBlockAfterHit = battleController.EnemyStats.Block;
                        req.targetStats = battleController.EnemyStats;
                    }
                    break;
                case "buff":
                case "debuff":
                    req.type = EffectType.Status;
                    if (battleController != null && battleController.EnemyStats != null)
                    {
                        req.visualHpAfterHit = battleController.EnemyStats.CurrentHp;
                        req.visualBlockAfterHit = battleController.EnemyStats.Block;
                        req.targetStats = battleController.EnemyStats;
                    }
                    break;
                default: return null;
            }
            return req;
        }

        // =====================================================================
        // 特效队列处理
        // =====================================================================

        private IEnumerator ProcessQueue()
        {
            isProcessing = true;

            // 特效序列期间禁用结束回合
            if (endTurnButton != null) endTurnButton.interactable = false;

            while (effectQueue.Count > 0)
            {
                var req = effectQueue.Dequeue();
                yield return StartCoroutine(PlayEffect(req));

                // 每个特效放完后刷新对应 UI
                RefreshUIForEffect(req);

                if (effectQueue.Count > 0)
                    yield return new WaitForSeconds(0.1f);
            }

            // 恢复结束回合
            if (endTurnButton != null && battleController != null)
                endTurnButton.interactable = battleController.Phase == BattleTurnPhase.PlayerTurn;

            // 特效全部播放完毕，解除视觉锁定，并强制刷新最终真实状态
            var pUI = FindObjectOfType<PlayerUI>(true);
            var eUI = FindObjectOfType<EnemyUI>(true);
            if (pUI != null)
            {
                pUI.HoldVisualStats = false;
                pUI.RefreshUI();
            }
            if (eUI != null)
            {
                eUI.HoldVisualStats = false;
                eUI.RefreshUI();
            }

            isProcessing = false;
        }

        private IEnumerator PlayEffect(EffectRequest req)
        {
            // GainBlock 不播放 Effect UI，而是直接缩放 BlockContainer
            if (req.type == EffectType.GainBlock)
            {
                yield return StartCoroutine(PlayGainBlockEffect(req));
                yield break;
            }

            RectTransform targetUI = GetEffectUI(req.type);
            AudioClip clip = GetEffectSound(req.type);
            RectTransform shakeTarget = req.isEnemy ? playerAvatar : enemyMonster;

            // 1. 放大入场
            if (targetUI != null)
            {
                targetUI.gameObject.SetActive(true);
                targetUI.localScale = Vector3.zero;
                targetUI.localRotation = req.isEnemy ? Quaternion.Euler(0f, 180f, 0f) : Quaternion.identity;
                Vector3 targetScale = Vector3.one;

                float elapsed = 0f;
                while (elapsed < effectScaleDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / effectScaleDuration);
                    float eased = 1f - (1f - t) * (1f - t);
                    targetUI.localScale = Vector3.Lerp(Vector3.zero, targetScale, eased);
                    yield return null;
                }
                targetUI.localScale = targetScale;
            }

            // 2. 音效
            if (clip != null)
                AudioManager.PlaySfxStatic(clip, Camera.main != null ? Camera.main.transform.position : Vector3.zero);

            // 音效响起、受击抖动开始的瞬间，立刻分步更新视觉血量/格挡
            UpdateVisualStats(req);

            // 3. 攻击抖动
            if (req.type == EffectType.Attack && shakeTarget != null)
                yield return StartCoroutine(ShakeTarget(shakeTarget));

            // 4. 停留
            yield return new WaitForSeconds(effectHoldDuration);

            // 5. 淡出
            if (targetUI != null)
            {
                CanvasGroup cg = targetUI.GetComponent<CanvasGroup>();
                if (cg == null) cg = targetUI.gameObject.AddComponent<CanvasGroup>();
                float startAlpha = 1f;
                float elapsed = 0f;
                while (elapsed < effectFadeDuration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    cg.alpha = Mathf.Lerp(startAlpha, 0f, Mathf.Clamp01(elapsed / effectFadeDuration));
                    yield return null;
                }
                cg.alpha = 1f;
                targetUI.gameObject.SetActive(false);
                targetUI.localScale = Vector3.zero;
            }
        }

        /// <summary>
        /// 获得格挡特效：弹跳缩放对应的 BlockContainer，配合 tap-a 音效。
        /// </summary>
        private IEnumerator PlayGainBlockEffect(EffectRequest req)
        {
            RectTransform blockContainer = req.isEnemy ? enemyBlockContainer : playerBlockContainer;
            AudioClip clip = gainBlockSound;

            if (blockContainer == null) yield break;

            // 记下原始缩放
            Vector3 originalScale = blockContainer.localScale;

            // 音效
            if (clip != null)
                AudioManager.PlaySfxStatic(clip, Camera.main != null ? Camera.main.transform.position : Vector3.zero);

            // 在音效响起的瞬间立刻更新视觉格挡
            UpdateVisualStats(req);

            // 弹跳放大 (1→1.3→1)
            float scaleTarget = Mathf.Clamp(1f + req.blockValue * 0.03f, 1.1f, 1.5f);
            float elapsed = 0f;
            float bounceDuration = effectScaleDuration * 0.6f;

            // 弹入
            while (elapsed < bounceDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / bounceDuration);
                float eased = 1f - (1f - t) * (1f - t);
                blockContainer.localScale = Vector3.Lerp(originalScale, Vector3.one * scaleTarget, eased);
                yield return null;
            }

            // 弹回
            elapsed = 0f;
            while (elapsed < bounceDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / bounceDuration);
                float eased = t * t; // ease-in
                blockContainer.localScale = Vector3.Lerp(Vector3.one * scaleTarget, originalScale, eased);
                yield return null;
            }
            blockContainer.localScale = originalScale;

            // 停留
            yield return new WaitForSeconds(effectHoldDuration);
        }

        private IEnumerator ShakeTarget(RectTransform target)
        {
            if (target == null) yield break;

            Vector3 originalPos = target.localPosition;
            float elapsed = 0f;
            while (elapsed < shakeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float x = Random.Range(-shakeIntensity, shakeIntensity);
                float y = Random.Range(-shakeIntensity, shakeIntensity);
                target.localPosition = originalPos + new Vector3(x, y, 0f);
                yield return null;
            }
            target.localPosition = originalPos;
        }

        // =====================================================================
        // UI 刷新 —— 每个特效放完后更新对应数值
        // =====================================================================

        private void RefreshUIForEffect(EffectRequest req)
        {
            // 特效播放完毕后，通知 UI 刷新。
            // PlayerStats / EnemyStats 的 OnStatsChanged / OnGlucoseChanged 事件
            // 已由 BattleHandDisplay / UltopController 等订阅，这里触发刷新即可。
        }

        private void UpdateVisualStats(EffectRequest req)
        {
            if (req.targetStats != null)
            {
                var playerUI = FindObjectOfType<PlayerUI>(true);
                var enemyUI = FindObjectOfType<EnemyUI>(true);
                if (req.targetStats is PlayerStats && playerUI != null)
                {
                    playerUI.SetVisualHpAndBlock(req.visualHpAfterHit, req.visualBlockAfterHit);
                }
                else if (req.targetStats is EnemyStats && enemyUI != null)
                {
                    enemyUI.SetVisualHpAndBlock(req.visualHpAfterHit, req.visualBlockAfterHit);
                }
            }
        }

        private RectTransform GetEffectUI(EffectType type)
        {
            switch (type)
            {
                case EffectType.Attack: return attackEffect;
                case EffectType.Defend: return defendEffect;
                case EffectType.Status: return statusEffect;
                default: return null;
            }
        }

        private AudioClip GetEffectSound(EffectType type)
        {
            switch (type)
            {
                case EffectType.Attack:    return attackSound;
                case EffectType.Defend:    return defendSound;
                case EffectType.GainBlock: return gainBlockSound;
                case EffectType.Status:    return statusSound;
                default: return null;
            }
        }

        private void AutoResolveReferences()
        {
            Transform bp = transform;
            if (attackEffect == null)   { var t = bp.Find("Effect/Effect_UI_Attack");  if (t) attackEffect = t as RectTransform; }
            if (defendEffect == null)   { var t = bp.Find("Effect/Effect_UI_Defend");  if (t) defendEffect = t as RectTransform; }
            if (statusEffect == null)   { var t = bp.Find("Effect/Effect_UI_Status");  if (t) statusEffect = t as RectTransform; }
            if (enemyMonster == null)   { var t = bp.Find("EnemyArea/Enemy_Stat/Enemy_Monster"); if (t) enemyMonster = t as RectTransform; }
            if (playerAvatar == null)   { var t = bp.Find("PlayerArea/Player_Stat/Player");       if (t) playerAvatar = t as RectTransform; }
            if (playerBlockContainer == null) { var t = bp.Find("PlayerArea/Player_Stat/BlockContainer"); if (t) playerBlockContainer = t as RectTransform; }
            if (enemyBlockContainer == null)  { var t = bp.Find("EnemyArea/Enemy_Stat/BlockContainer");   if (t) enemyBlockContainer = t as RectTransform; }
            if (endTurnButton == null)  { var t = bp.Find("ActionArea/EndTurn_Button"); if (t) endTurnButton = t.GetComponent<Button>(); }

            if (attackSound == null)    attackSound    = Resources.Load<AudioClip>("Audio/Battle_Attack");
            if (defendSound == null)    defendSound    = Resources.Load<AudioClip>("Audio/Battle_Defend");
            if (gainBlockSound == null) gainBlockSound = Resources.Load<AudioClip>("Audio/Battle_GainBlock");
            if (statusSound == null)    statusSound    = Resources.Load<AudioClip>("Audio/Battle_Status");
        }

        // =====================================================================
        // 内部类型
        // =====================================================================

        private enum EffectType { Attack, Defend, GainBlock, Status }

        private struct EffectRequest
        {
            public EffectType type;
            public bool isEnemy;
            public int blockValue; // GainBlock 时的格挡值，用于缩放
            public int visualHpAfterHit;   // 受击后的视觉 HP
            public int visualBlockAfterHit; // 受击后的视觉格挡值
            public EntityStats targetStats; // 目标 Stats 引用
        }

        private static EffectRequest EffectReq(EffectType t, bool enemy, int bv = 0)
        {
            return new EffectRequest { type = t, isEnemy = enemy, blockValue = bv };
        }
    }
}
