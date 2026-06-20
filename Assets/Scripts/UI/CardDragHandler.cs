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
    public class CardDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler
    {
        private Canvas _canvas;
        private CanvasGroup _canvasGroup;
        private RectTransform _rectTransform;
        private RectTransform _hoverDetectorRect; // Hover 判定区 RectTransform
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
        private static bool _isAnyCardDragging = false; // 静态全局变量，用于在任何卡牌处于拖拽时屏蔽其他卡牌的 Hover
        private Canvas _canvasComponent;
        private bool _isHovered;
        private float _defaultY;
        private float _defaultLocalY;
        private Vector3 _defaultScale;
        private Coroutine _hoverCoroutine;
        private const float HoverScale = 1.15f;      // 放大系数 (1.15倍)
        private const float HoverYOffset = 50f;      // 向上浮动像素
        private const float HoverDuration = 0.15f;   // 缓动时间 (0.15秒)

        [Header("展示专用模式")]
        [SerializeField] private bool _isDisplayOnly = false;

        public void SetDisplayOnly(bool val) { _isDisplayOnly = val; }
        public bool IsDisplayOnly => _isDisplayOnly;
        public CardInfo CardInfo => _cardInfo;
        public void SetCardInfo(CardInfo card) { _cardInfo = card; }

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            _canvasGroup = GetComponent<CanvasGroup>();
            _cardUI = GetComponent<CardUI>();
            // 查找父级 Canvas（跳过卡牌自身的 Sub-Canvas）
            _canvas = transform.parent != null ? transform.parent.GetComponentInParent<Canvas>() : GetComponentInParent<Canvas>();

            // 查找专门的 Hover 判定区域 HoverDetector（其大小更接近卡牌的真实视觉边界，避免因外围透明 padding 导致判定重叠）
            Transform detectorTrans = transform.Find("HoverDetector");
            if (detectorTrans != null)
            {
                _hoverDetectorRect = detectorTrans.GetComponent<RectTransform>();
            }
            else
            {
                _hoverDetectorRect = _rectTransform;
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

            // 仅对非展示用的手牌初始化 Canvas，以便支持置顶
            if (!_isDisplayOnly)
            {
                _canvasComponent = GetComponent<Canvas>();
                if (_canvasComponent == null)
                {
                    _canvasComponent = gameObject.AddComponent<Canvas>();
                }
                _canvasComponent.overrideSorting = false;

                // 嵌套 Canvas 必须具有 GraphicRaycaster，否则其子物体无法接收 UGUI 事件系统射线拦截
                var gr = GetComponent<GraphicRaycaster>();
                if (gr == null)
                {
                    gr = gameObject.AddComponent<GraphicRaycaster>();
                }
            }
            else
            {
                // 展示用卡牌必须销毁身上的 Canvas 与 Raycaster，从而使其受 ScrollView 视口遮罩裁剪和射线屏蔽
                var c = GetComponent<Canvas>();
                if (c != null) Destroy(c);
                var gr = GetComponent<GraphicRaycaster>();
                if (gr != null) Destroy(gr);
            }

            // 确保 HoverDetector 成为唯一的射线拦截目标，阻止父级大透明框拦截导致误进入 Hover
            if (_hoverDetectorRect != null && _hoverDetectorRect != _rectTransform)
            {
                // 1. 关闭除 HoverDetector（及其子物体）之外的所有 Image/TextMeshProUGUI 的 raycastTarget
                var images = GetComponentsInChildren<Image>(true);
                foreach (var img in images)
                {
                    if (img.transform == _hoverDetectorRect || img.transform.IsChildOf(_hoverDetectorRect))
                    {
                        img.raycastTarget = true;
                    }
                    else
                    {
                        img.raycastTarget = false;
                    }
                }

                var texts = GetComponentsInChildren<TMPro.TextMeshProUGUI>(true);
                foreach (var txt in texts)
                {
                    if (txt.transform == _hoverDetectorRect || txt.transform.IsChildOf(_hoverDetectorRect))
                    {
                        txt.raycastTarget = true;
                    }
                    else
                    {
                        txt.raycastTarget = false;
                    }
                }

                // 2. 确保 HoverDetector 本身可以接收射线
                var hdImage = _hoverDetectorRect.GetComponent<Image>();
                if (hdImage == null)
                {
                    hdImage = _hoverDetectorRect.gameObject.AddComponent<Image>();
                    hdImage.color = new Color(0, 0, 0, 0);
                }
                hdImage.raycastTarget = true;
            }
        }

        private bool CanTargetEnemy() =>
            _cardInfo != null && (_cardInfo.finalDamage > 0 || _cardInfo.HasEffect("apply_debuff"));

        private bool CanTargetSelf() =>
            _cardInfo != null && !CanTargetEnemy();

        public void OnPointerDown(PointerEventData eventData)
        {
            Debug.Log($"[CardDragHandler] OnPointerDown called on card: {_cardInfo?.name ?? "null"}. button.interactable={GetComponent<Button>()?.interactable}");
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            Debug.Log($"[CardDragHandler] OnBeginDrag called on card: {_cardInfo?.name ?? "null"}");
            if (_isDisplayOnly) { Debug.Log("[CardDragHandler] Drag Blocked: _isDisplayOnly is true"); return; }
            if (_cardInfo == null) { Debug.Log("[CardDragHandler] Drag Blocked: _cardInfo is null"); return; }
            if (_battleController == null) { Debug.Log("[CardDragHandler] Drag Blocked: _battleController is null"); return; }
            
            bool canPlay = _battleController.CanPlayCard(_cardInfo);
            Debug.Log($"[CardDragHandler] CanPlayCard check: {canPlay}. Phase={_battleController.Phase}, CurrentEnergy={_battleController.CurrentEnergy}, CardCost={_cardInfo.energyCost}");
            if (!canPlay) return;

            bool isHandDisplayAnimating = _handDisplay != null && _handDisplay.IsAnimating;
            Debug.Log($"[CardDragHandler] HandDisplay IsAnimating check: {isHandDisplayAnimating}");
            if (isHandDisplayAnimating) return;

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
                _rectTransform.anchoredPosition = new Vector2(_rectTransform.anchoredPosition.x, _defaultY);
                if (_canvasComponent != null) _canvasComponent.overrideSorting = false;
            }

            _isDragging = true;
            _isAnyCardDragging = true;
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
            if (_isDisplayOnly) return;
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
            if (_isDisplayOnly) return;
            if (!_isDragging) return;
            _isDragging = false;
            _isAnyCardDragging = false;

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
                Debug.Log($"[CardDragHandler] OnEndDrag: Card not played. lockState={_lockState}, CanPlay={(_battleController != null && _cardInfo != null && _battleController.CanPlayCard(_cardInfo))}");
                if (clone != null) Destroy(clone);
                _rectTransform.SetParent(_originalParent);
                _rectTransform.SetSiblingIndex(_originalSiblingIndex);
                _rectTransform.anchoredPosition = _originalPosition;
            }
        }

        private bool IsOver(RectTransform rect, Vector2 screenPos)
        {
            if (rect == null) return false;
            Camera uiCamera = (_canvas != null && _canvas.renderMode == RenderMode.ScreenSpaceOverlay) ? null : _canvas.worldCamera;
            return RectTransformUtility.RectangleContainsScreenPoint(rect, screenPos, uiCamera);
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

        // InitDefaultY is no longer needed since positions are captured dynamically on hover enter.

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
            Debug.Log($"[CardDragHandler] OnHoverEnter called on card: {_cardInfo?.name ?? "null"}");
            if (_isAnyCardDragging) { Debug.Log("[CardDragHandler] Hover Blocked: _isAnyCardDragging is true"); return; }
            if (_isDragging || _cardInfo == null) { Debug.Log("[CardDragHandler] Hover Blocked: _isDragging is true or _cardInfo is null"); return; }
            if (_isHovered) return;

            if (!_isDisplayOnly)
            {
                bool canPlay = _battleController != null && _battleController.CanPlayCard(_cardInfo);
                Debug.Log($"[CardDragHandler] Hover CanPlayCard check: {canPlay}. Phase={_battleController?.Phase}, CurrentEnergy={_battleController?.CurrentEnergy}, CardCost={_cardInfo?.energyCost}");
                if (!canPlay) return;
            }

            // 动画中（如抽牌/弃牌飞入飞出时）禁止 Hover
            var anim = GetComponent<CardAnimator>();
            if (anim != null && anim.IsAnimating) { Debug.Log("[CardDragHandler] Hover Blocked: CardAnimator.IsAnimating is true"); return; }
            if (_handDisplay != null && _handDisplay.IsAnimating) { Debug.Log("[CardDragHandler] Hover Blocked: HandDisplay.IsAnimating is true"); return; }

            // 动态捕获当前无 Hover 状态下的锚点坐标与本地坐标，保证在布局变动后依然计算精准
            _defaultY = _rectTransform.anchoredPosition.y;
            _defaultLocalY = _rectTransform.localPosition.y;
            _defaultScale = _rectTransform.localScale;
            _isHovered = true;

            // 播放卡牌 Hover 音效
            AudioClip cardHoverSound = Resources.Load<AudioClip>("Audio/Card_Hover");
            if (cardHoverSound != null)
            {
                Vector3 pos = Camera.main != null ? Camera.main.transform.position : transform.position;
                CGM.Core.AudioManager.PlaySfxStatic(cardHoverSound, pos);
            }

            // 只有非展示模式（即手牌中）才启用渲染层置顶，避免 ScrollView 中的卡牌遮挡/裁剪失效
            if (!_isDisplayOnly && _canvasComponent != null)
            {
                _canvasComponent.overrideSorting = true;
                _canvasComponent.sortingOrder = 30;
            }

            float targetY = _defaultY;
            if (!_isDisplayOnly)
            {
                targetY += HoverYOffset;
            }

            StartHoverAnimation(HoverScale, targetY);
            TryShowTooltip();
        }

        public void OnHoverExit(PointerEventData eventData)
        {
            // 移出卡牌
            if (!_isHovered) return;

            // 如果鼠标实际上还在卡牌的原始或偏移延展区域内（由 Y 轴偏移导致的假退出），不进行 Exit 处理
            if (eventData != null && IsMouseOverExtendedBounds())
            {
                return;
            }

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
                // 仅在自己是描述框当前的拥有者时，才执行关闭隐藏，防止相互踩踏
                TooltipManager.Instance.HideTooltip(transform as RectTransform);
            }
        }

        private void Update()
        {
            if (_isHovered && !IsMouseOverExtendedBounds())
            {
                OnHoverExit(null);
            }
        }

        private bool IsMouseOverExtendedBounds()
        {
            if (_canvas == null || _hoverDetectorRect == null) return false;

            Vector2 mousePos = Input.mousePosition;
            Camera uiCamera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
            Vector2 localPosDetector;
            // 将屏幕坐标转换至 HoverDetector 的本地坐标空间内
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_hoverDetectorRect, mousePos, uiCamera, out localPosDetector))
            {
                float halfWidth = _hoverDetectorRect.rect.width * 0.5f;
                float halfHeight = _hoverDetectorRect.rect.height * 0.5f;

                // 当前 Y 轴相对于默认/静止本地 Y 的偏移量
                float currentYOffset = _rectTransform.localPosition.y - _defaultLocalY;
                // 将偏移量转换为相对于 HoverDetector (及卡牌) 缩放后的本地坐标空间偏移
                float localYOffset = currentYOffset / _rectTransform.localScale.y;

                // 允许的 X 范围就是 HoverDetector 的宽度范围
                bool xOverlap = localPosDetector.x >= -halfWidth && localPosDetector.x <= halfWidth;

                // 允许的 Y 范围是：从静止时的最底部（-halfHeight - localYOffset），到当前的最顶部（halfHeight）
                float minY = -halfHeight - Mathf.Max(0f, localYOffset);
                float maxY = halfHeight - Mathf.Min(0f, localYOffset);

                return xOverlap && localPosDetector.y >= minY && localPosDetector.y <= maxY;
            }
            return false;
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
            Vector3 endScale = _defaultScale * targetScale;

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
            if (TooltipManager.Instance != null && _cardInfo != null)
            {
                TooltipManager.Instance.ShowCardEffectsTooltip(_cardInfo, transform as RectTransform);
            }
        }
    }
}

