using UnityEngine;
using UnityEngine.EventSystems;

namespace CGM.UI
{
    /// <summary>
    /// CGM.UI.CardHoverTrigger — 卡牌悬停检测转发器
    /// 职责：挂载在卡牌预制体内部手动划分的“中心判定区”空物体上，将 Pointer 划过事件精准向上传递给父级 CardDragHandler。
    ///       这可以有效缩小 Hover 判定范围，排除卡牌四周透明区域的重叠干扰。
    /// </summary>
    public class CardHoverTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private CardDragHandler _parentHandler;

        private void Awake()
        {
            _parentHandler = GetComponentInParent<CardDragHandler>();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_parentHandler != null)
            {
                _parentHandler.OnHoverEnter(eventData);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_parentHandler != null)
            {
                _parentHandler.OnHoverExit(eventData);
            }
        }
    }
}
