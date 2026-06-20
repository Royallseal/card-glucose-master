using UnityEngine;
using UnityEngine.EventSystems;

namespace CGM.UI
{
    /// <summary>
    /// CGM.UI.CardHoverTrigger — 卡牌悬停检测与事件转发器
    /// 职责：挂载在卡牌预制体内部手动划分的“中心判定区”空物体上，将 Pointer 划过、点击、拖拽等事件精准向上传递给父级 CardDragHandler。
    ///       这可以有效缩小 Hover 判定范围，排除卡牌四周透明区域的重叠干扰，并解决 UGUI 事件冒泡可能被截断的问题。
    /// </summary>
    public class CardHoverTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private CardDragHandler _parentHandler;

        private CardDragHandler GetParentHandler()
        {
            if (_parentHandler == null)
            {
                _parentHandler = GetComponentInParent<CardDragHandler>();
            }
            return _parentHandler;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            Debug.Log($"[CardHoverTrigger] OnPointerEnter called on: {gameObject.name}");
            var handler = GetParentHandler();
            if (handler != null)
            {
                handler.OnHoverEnter(eventData);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            Debug.Log($"[CardHoverTrigger] OnPointerExit called on: {gameObject.name}");
            var handler = GetParentHandler();
            if (handler != null)
            {
                handler.OnHoverExit(eventData);
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            Debug.Log($"[CardHoverTrigger] OnPointerDown called on: {gameObject.name}");
            var handler = GetParentHandler();
            if (handler != null)
            {
                handler.OnPointerDown(eventData);
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            Debug.Log($"[CardHoverTrigger] OnBeginDrag called on: {gameObject.name}");
            var handler = GetParentHandler();
            if (handler != null)
            {
                handler.OnBeginDrag(eventData);
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            var handler = GetParentHandler();
            if (handler != null)
            {
                handler.OnDrag(eventData);
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            Debug.Log($"[CardHoverTrigger] OnEndDrag called on: {gameObject.name}");
            var handler = GetParentHandler();
            if (handler != null)
            {
                handler.OnEndDrag(eventData);
            }
        }
    }
}
