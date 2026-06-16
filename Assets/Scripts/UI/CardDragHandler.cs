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
    public class CardDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
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

        private GameObject _dragClone;
        private RectTransform _dragCloneRect;

        private DragLockState _lockState;
        private RectTransform _enemyDetectRect;
        private RectTransform _playerDetectRect;
        private bool _isDragging;

        public void SetCardInfo(CardInfo card) { _cardInfo = card; }

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            _canvasGroup = GetComponent<CanvasGroup>();
            _cardUI = GetComponent<CardUI>();
            _canvas = GetComponentInParent<Canvas>();
        }

        private void Start()
        {
            _playerStats = FindObjectOfType<PlayerStats>();
            _enemyStats = FindObjectOfType<EnemyStats>();
            _battleController = FindObjectOfType<BattleSessionController>();

            var go = GameObject.Find("Enemy_Stat");
            if (go != null) _enemyDetectRect = go.GetComponent<RectTransform>();
            go = GameObject.Find("Player_Stat");
            if (go != null) _playerDetectRect = go.GetComponent<RectTransform>();
        }

        private bool CanTargetEnemy() =>
            _cardInfo != null && (_cardInfo.finalDamage > 0 || _cardInfo.HasEffect("apply_debuff"));

        private bool CanTargetSelf() =>
            _cardInfo != null && (_cardInfo.finalBlock > 0 || _cardInfo.HasEffect("apply_buff")
                || _cardInfo.HasEffect("draw") || _cardInfo.HasEffect("glucose_cap"));

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_cardInfo == null || _battleController == null) return;
            if (!_battleController.CanPlayCard(_cardInfo)) return;

            _isDragging = true;
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
            if (!_isDragging) return;
            _isDragging = false;
            CleanupDrag();

            bool played = false;

            if (_lockState == DragLockState.LockedOnEnemy && _battleController != null && _battleController.CanPlayCard(_cardInfo))
            {
                _battleController.PlayCard(_cardInfo, _enemyStats);
                played = true;
            }
            else if (_lockState == DragLockState.LockedOnPlayer && _battleController != null && _battleController.CanPlayCard(_cardInfo))
            {
                _battleController.PlayCard(_cardInfo, _playerStats);
                played = true;
            }

            _lockState = DragLockState.None;
            UpdateCardFace();

            if (!played)
            {
                _rectTransform.SetParent(_originalParent);
                _rectTransform.SetSiblingIndex(_originalSiblingIndex);
                _rectTransform.anchoredPosition = _originalPosition;
            }
        }

        private void CleanupDrag()
        {
            if (_dragClone != null) { Destroy(_dragClone); _dragClone = null; _dragCloneRect = null; }
            SetIndicator(DragLockState.LockedOnEnemy, false);
            SetIndicator(DragLockState.LockedOnPlayer, false);
            _canvasGroup.alpha = 1f;
            _canvasGroup.blocksRaycasts = true;
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

            _cardUI.SetCard(_cardInfo, dmgMod, blkMod);
        }
    }
}
