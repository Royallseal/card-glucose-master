// =============================================================================
// CardDragHandler.cs — 卡牌拖拽控制器
// 支持区分敌我目标、锁定状态实时计算卡面数值。
// =============================================================================

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using CGM.Core;
using CGM.Data;

namespace CGM.UI
{
    public enum DragLockState { None, LockedOnEnemy, LockedOnPlayer }

    [RequireComponent(typeof(CanvasGroup))]
    public class CardDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
    {
        private Canvas _canvas;
        private CanvasGroup _canvasGroup;
        private RectTransform _rectTransform;
        private CardUI _cardUI;
        private Vector2 _originalPosition;
        private Transform _originalParent;
        private int _originalSiblingIndex;

        private CardInfo _cardInfo;
        private PlayerStats _playerStats;
        private EnemyStats _enemyStats;
        private BattleSessionController _battleController;
        private BattleHandDisplay _handDisplay;

        private GameObject _dragClone;
        private RectTransform _dragCloneRect;

        private DragLockState _lockState;
        private RectTransform _enemyDetectRect;
        private RectTransform _playerDetectRect;
        public bool DraggedAndPlayed { get; set; }
        private bool _isDragging;

        // Hover 悬停动效配置
        private static bool IsAnyCardDragging = false; // 静态全局变量，用于在任何卡牌处于拖拽时屏蔽其他卡牌的 Hover
        private Canvas _canvasComponent;
        private bool _isHovered;
        private bool _hasInitializedDefaultY;
        private float _defaultY;
        private Coroutine _hoverCoroutine;
        private const float HoverScale = 1.15f;      // 放大系数 (1.15倍)
        private const float HoverYOffset = 50f;      // 向上浮动像素
        private const float HoverDuration = 0.15f;   // 缓动时间 (0.15秒)

        [Header("展示专用模式")]
        [SerializeField] private bool isDisplayOnly = false;

        public void SetDisplayOnly(bool val) { isDisplayOnly = val; }
        public bool IsDisplayOnly => isDisplayOnly;
        public CardInfo CardInfo => _cardInfo;
        public void SetCardInfo(CardInfo card) { _cardInfo = card; }

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            _canvasGroup = GetComponent<CanvasGroup>();
            _cardUI = GetComponent<CardUI>();
            _canvas = GetComponentInParent<Canvas>();

            // 动态初始化 Sub-Canvas 重排序组件，用于实现置顶且不破坏布局
            _canvasComponent = GetComponent<Canvas>();
            if (_canvasComponent == null)
            {
                _canvasComponent = gameObject.AddComponent<Canvas>();
            }
            _canvasComponent.overrideSorting = false;

            if (GetComponent<GraphicRaycaster>() == null)
            {
                gameObject.AddComponent<GraphicRaycaster>();
            }
        }

        private void Start()
        {
            _playerStats = FindObjectOfType<PlayerStats>();
            _enemyStats = FindObjectOfType<EnemyStats>();
            _battleController = FindObjectOfType<BattleSessionController>();
            _handDisplay = FindObjectOfType<BattleHandDisplay>();

            var go = GameObject.Find("Enemy_Stat");
            if (go != null) _enemyDetectRect = go.GetComponent<RectTransform>();
            go = GameObject.Find("Player_Stat");
            if (go != null) _playerDetectRect = go.GetComponent<RectTransform>();
        }

        private bool CanTargetEnemy() =>
            _cardInfo != null && (_cardInfo.finalDamage > 0 || _cardInfo.HasEffect("apply_debuff"));

        private bool CanTargetSelf() =>
            _cardInfo != null && !CanTargetEnemy();

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (isDisplayOnly) return;
            if (_cardInfo == null || _battleController == null) return;
            if (!_battleController.CanPlayCard(_cardInfo)) return;
            if (_handDisplay != null && _handDisplay.IsAnimating) return;

            if (TooltipManager.Instance != null)
            {
                TooltipManager.Instance.HideTooltip();
            }

            // 如果正在 Hover，瞬间还原并停止动效，避免把 Hover 的状态带入拖拽克隆
            if (_isHovered)
            {
                _isHovered = false;
                if (_hoverCoroutine != null) StopCoroutine(_hoverCoroutine);
                _rectTransform.localScale = Vector3.one;
                InitDefaultY();
                _rectTransform.anchoredPosition = new Vector2(_rectTransform.anchoredPosition.x, _defaultY);
                if (_canvasComponent != null) _canvasComponent.overrideSorting = false;
            }

            _isDragging = true;
            IsAnyCardDragging = true;
            _originalPosition = _rectTransform.anchoredPosition;
            _originalParent = _rectTransform.parent;
            _originalSiblingIndex = _rectTransform.GetSiblingIndex();
            _lockState = DragLockState.None;

            _dragClone = Instantiate(gameObject, _canvas.transform);
            _dragClone.name = "DragClone";
            _dragCloneRect = _dragClone.GetComponent<RectTransform>();
            Destroy(_dragClone.GetComponent<CardDragHandler>());
            Destroy(_dragClone.GetComponent<Button>());
            var cg = _dragClone.GetComponent<CanvasGroup>();
            if (cg != null) { cg.alpha = 0.7f; cg.blocksRaycasts = false; }
            _dragCloneRect.SetAsLastSibling();

            _canvasGroup.alpha = 0.4f;
            _canvasGroup.blocksRaycasts = false;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (isDisplayOnly) return;
            if (_dragCloneRect == null) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvas.transform as RectTransform, eventData.position, _canvas.worldCamera, out Vector2 localPos);
            _dragCloneRect.localPosition = localPos;

            DragLockState newState = DragLockState.None;
            bool overEnemy = IsOver(_enemyDetectRect, eventData.position);
            bool overPlayer = IsOver(_playerDetectRect, eventData.position);

            if (overEnemy && CanTargetEnemy()) newState = DragLockState.LockedOnEnemy;
            else if (overPlayer && CanTargetSelf()) newState = DragLockState.LockedOnPlayer;
            
            if (newState != _lockState)
            {
                SetIndicator(DragLockState.LockedOnEnemy, newState == DragLockState.LockedOnEnemy);
                SetIndicator(DragLockState.LockedOnPlayer, newState == DragLockState.LockedOnPlayer);
                _lockState = newState;
                UpdateCardFace();
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (isDisplayOnly) return;
            if (!_isDragging) return;
            _isDragging = false;
            IsAnyCardDragging = false;

            // 保存克隆引用，稍后决定动画
            var clone = _dragClone;
            _dragClone = null;
            _dragCloneRect = null;

            // 恢复原卡牌
            _canvasGroup.alpha = 1f;
            _canvasGroup.blocksRaycasts = true;
            SetIndicator(DragLockState.LockedOnEnemy, false);
            SetIndicator(DragLockState.LockedOnPlayer, false);

            bool played = false;

            if (_lockState == DragLockState.LockedOnEnemy && _battleController != null && _battleController.CanPlayCard(_cardInfo))
            {
                this.DraggedAndPlayed = true;
                _battleController.PlayCard(_cardInfo, _enemyStats);
                played = true;
            }
            else if (_lockState == DragLockState.LockedOnPlayer && _battleController != null && _battleController.CanPlayCard(_cardInfo))
            {
                this.DraggedAndPlayed = true;
                _battleController.PlayCard(_cardInfo, _playerStats);
                played = true;
            }

            _lockState = DragLockState.None;
            UpdateCardFace();

            // 确保拖拽结束后 Hover 状态完全复位
            _isHovered = false;
            if (_hoverCoroutine != null) StopCoroutine(_hoverCoroutine);
            if (_canvasComponent != null) _canvasComponent.overrideSorting = false;

            if (played)
            {
                // 克隆体飞到弃牌堆
                var discardPile = GameObject.Find("DiscardPile_UI")?.GetComponent<RectTransform>();
                if (clone != null && discardPile != null)
                {
                    var anim = clone.GetComponent<CardAnimator>();
                    if (anim == null) anim = clone.AddComponent<CardAnimator>();
                    anim.PlayDiscardAnimation(discardPile.position);
                }
                else if (clone != null)
                {
                    Destroy(clone);
                }
            }
            else
            {
                if (clone != null) Destroy(clone);
                _rectTransform.SetParent(_originalParent);
                _rectTransform.SetSiblingIndex(_originalSiblingIndex);
                _rectTransform.anchoredPosition = _originalPosition;
            }
        }

        private bool IsOver(RectTransform rect, Vector2 screenPos)
        {
            if (rect == null) return false;
            return RectTransformUtility.RectangleContainsScreenPoint(rect, screenPos, _canvas.worldCamera);
        }

        private void SetIndicator(DragLockState which, bool show)
        {
            if (which == DragLockState.LockedOnEnemy)
            {
                var eui = _enemyStats != null ? _enemyStats.GetComponent<EnemyUI>() : null;
                if (eui != null) eui.ShowTargetIndicator(show);
            }
            else if (which == DragLockState.LockedOnPlayer)
            {
                var pui = _playerStats != null ? _playerStats.GetComponent<PlayerUI>() : null;
                if (pui != null) pui.ShowTargetIndicator(show);
            }
        }

        private void UpdateCardFace()
        {
            if (_cardUI == null || _cardInfo == null || _playerStats == null) return;
            int dmgMod, blkMod;

            if (_lockState == DragLockState.LockedOnEnemy && _enemyStats != null)
            {
                int full = BattleCalculator.CalculateDamage(_cardInfo, _playerStats, _enemyStats);
                dmgMod = full - _cardInfo.finalDamage;
            }
            else
            {
                int self = BattleCalculator.CalculateSelfDamage(_cardInfo, _playerStats);
                dmgMod = self - _cardInfo.finalDamage;
            }

            int sBlk = BattleCalculator.CalculateSelfBlock(_cardInfo, _playerStats);
            blkMod = sBlk - _cardInfo.finalBlock;

            float glucoseMultiplier = _playerStats != null ? BattleCalculator.GetGlucoseChangeMultiplier(_playerStats) : 1.0f;
            _cardUI.SetCard(_cardInfo, dmgMod, blkMod, glucoseMultiplier);
        }

        // =========================================================================
        // Hover 悬停置顶动效实现
        // =========================================================================

        private void InitDefaultY()
        {
            if (!_hasInitializedDefaultY)
            {
                _defaultY = _rectTransform.anchoredPosition.y;
                _hasInitializedDefaultY = true;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            OnHoverEnter(eventData);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            OnHoverExit(eventData);
        }

        public void OnHoverEnter(PointerEventData eventData)
        {
            Debug.Log($"[CardDragHandler] OnHoverEnter on card: {(_cardInfo != null ? _cardInfo.name : "null")} (isDisplayOnly: {isDisplayOnly})");
            if (IsAnyCardDragging)
            {
                Debug.Log("[CardDragHandler] Hover ignored: another card is dragging.");
                return;
            }
            if (_isDragging)
            {
                Debug.Log("[CardDragHandler] Hover ignored: this card is dragging.");
                return;
            }
            if (_cardInfo == null)
            {
                Debug.LogWarning("[CardDragHandler] Hover ignored: _cardInfo is null.");
                return;
            }

            if (!isDisplayOnly)
            {
                if (_battleController == null)
                {
                    Debug.LogWarning("[CardDragHandler] Hover ignored: _battleController is null.");
                    return;
                }
                if (!_battleController.CanPlayCard(_cardInfo))
                {
                    Debug.Log("[CardDragHandler] Hover ignored: Cannot play card.");
                    return;
                }
            }

            // 动画中（如抽牌/弃牌飞入飞出时）禁止 Hover
            var anim = GetComponent<CardAnimator>();
            if (anim != null && anim.IsAnimating)
            {
                Debug.Log("[CardDragHandler] Hover ignored: CardAnimator is animating.");
                return;
            }
            if (_handDisplay != null && _handDisplay.IsAnimating)
            {
                Debug.Log("[CardDragHandler] Hover ignored: BattleHandDisplay is animating.");
                return;
            }

            InitDefaultY();
            _isHovered = true;
            Debug.Log($"[CardDragHandler] Card hovered successfully. Name: {_cardInfo.name}");

            // 播放卡牌 Hover 音效
            AudioClip cardHoverSound = Resources.Load<AudioClip>("Audio/Card_Hover");
            if (cardHoverSound != null)
            {
                Vector3 pos = Camera.main != null ? Camera.main.transform.position : transform.position;
                AudioSource.PlayClipAtPoint(cardHoverSound, pos, 0.8f);
            }

            // 启用渲染层置顶，利用 Sub-Canvas 的 overrideSorting 保证它叠在左右邻近卡牌上方而不影响 Layout 排版顺序
            if (_canvasComponent != null)
            {
                _canvasComponent.overrideSorting = true;
                _canvasComponent.sortingOrder = 30;
            }

            StartHoverAnimation(HoverScale, _defaultY + HoverYOffset);
            TryShowTooltip();
        }

        public void OnHoverExit(PointerEventData eventData)
        {
            Debug.Log($"[CardDragHandler] OnHoverExit on card: {(_cardInfo != null ? _cardInfo.name : "null")}");
            if (!_isHovered) return;
            _isHovered = false;

            StartHoverAnimation(1.0f, _defaultY, () => {
                // 还原完毕后，释放排序权限归还默认层级，避免阻碍其他 UI 渲染
                if (!_isHovered && _canvasComponent != null)
                {
                    _canvasComponent.overrideSorting = false;
                }
            });

            if (TooltipManager.Instance != null)
            {
                TooltipManager.Instance.HideTooltip();
            }
        }

        private void StartHoverAnimation(float targetScale, float targetY, System.Action onComplete = null)
        {
            if (_hoverCoroutine != null)
            {
                StopCoroutine(_hoverCoroutine);
            }
            _hoverCoroutine = StartCoroutine(HoverRoutine(targetScale, targetY, onComplete));
        }

        private System.Collections.IEnumerator HoverRoutine(float targetScale, float targetY, System.Action onComplete)
        {
            Vector3 startScale = _rectTransform.localScale;
            float startY = _rectTransform.anchoredPosition.y;
            Vector3 endScale = Vector3.one * targetScale;

            float t = 0f;
            while (t < HoverDuration)
            {
                t += Time.deltaTime;
                float p = Mathf.Clamp01(t / HoverDuration);
                float eased = 1f - (1f - p) * (1f - p); // Ease-out quad

                _rectTransform.localScale = Vector3.Lerp(startScale, endScale, eased);
                Vector2 pos = _rectTransform.anchoredPosition;
                pos.y = Mathf.Lerp(startY, targetY, eased);
                _rectTransform.anchoredPosition = pos;

                yield return null;
            }

            _rectTransform.localScale = endScale;
            Vector2 finalPos = _rectTransform.anchoredPosition;
            finalPos.y = targetY;
            _rectTransform.anchoredPosition = finalPos;

            onComplete?.Invoke();
            _hoverCoroutine = null;
        }

        private void TryShowTooltip()
        {
            Debug.Log($"[CardDragHandler] TryShowTooltip for card: {(_cardInfo != null ? _cardInfo.name : "null")}");
            if (TooltipManager.Instance == null)
            {
                Debug.LogWarning("[CardDragHandler] TooltipManager.Instance is null!");
                return;
            }
            if (_cardInfo == null)
            {
                Debug.LogWarning("[CardDragHandler] _cardInfo is null!");
                return;
            }
            if (_cardInfo.effects == null)
            {
                Debug.LogWarning("[CardDragHandler] _cardInfo.effects is null!");
                return;
            }

            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            int count = 0;

            foreach (var effect in _cardInfo.effects)
            {
                if (effect.effectType == "apply_buff" || effect.effectType == "apply_debuff")
                {
                    try
                    {
                        BuffId buffId = effect.GetBuffId();
                        var buffInfo = BuffDatabase.Get(buffId);
                        if (buffInfo != null)
                        {
                            if (count > 0) sb.Append("\n\n");
                            sb.Append($"<color={buffInfo.colorHex}><b>{buffInfo.name}</b></color>\n{buffInfo.description}");
                            count++;
                        }
                        else
                        {
                            Debug.LogWarning($"[CardDragHandler] BuffDatabase has no entry for buffId: {buffId}");
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"[CardDragHandler] Exception parsing effect buff: {ex.Message}");
                    }
                }
            }

            Debug.Log($"[CardDragHandler] Buff/Debuff effect count found: {count}");
            if (count > 0)
            {
                TooltipManager.Instance.ShowTooltip(sb.ToString(), transform as RectTransform);
            }
            else
            {
                Debug.Log("[CardDragHandler] This card does not have any apply_buff or apply_debuff effects. No tooltip will be shown.");
            }
        }
    }
}
