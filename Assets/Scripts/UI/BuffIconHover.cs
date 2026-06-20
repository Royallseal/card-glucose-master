// =============================================================================
// BuffIconHover.cs — Buff 图标悬停描述
// =============================================================================

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using CGM.Data;

namespace CGM.UI
{
    /// <summary>
    /// 挂载在 Buff 图标预制体上。鼠标悬停时显示对应状态的描述文字。
    /// 描述框子节点默认名称为 "DescText"，也可通过 Inspector 指定。
    /// </summary>
    public class BuffIconHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private GameObject descPanel;
        [SerializeField] private TextMeshProUGUI descText;
        [SerializeField] private BuffId buffId;

        public void Setup(BuffId id)
        {
            buffId = id;
            if (descPanel != null) descPanel.SetActive(false);
            if (TooltipManager.Instance != null) TooltipManager.Instance.HideTooltip(transform as RectTransform);
        }

        private void Awake()
        {
            if (descPanel == null)
            {
                var t = transform.Find("BuffDescribe");
                if (t != null) descPanel = t.gameObject;
            }
            if (descText == null && descPanel != null)
                descText = descPanel.GetComponentInChildren<TextMeshProUGUI>();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            var info = BuffDatabase.Get(buffId);
            if (info == null) return;

            string content = $"<color={info.colorHex}><b>{info.name}</b></color>\n{info.description}";

            if (TooltipManager.Instance != null)
            {
                TooltipManager.Instance.ShowTooltip(content, transform as RectTransform);
            }
            else if (descPanel != null && descText != null)
            {
                descText.text = content;
                descPanel.SetActive(true);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (TooltipManager.Instance != null)
            {
                TooltipManager.Instance.HideTooltip(transform as RectTransform);
            }
            else if (descPanel != null)
            {
                descPanel.SetActive(false);
            }
        }
    }
}
