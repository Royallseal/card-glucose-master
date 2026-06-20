using UnityEngine;
using UnityEngine.EventSystems;
using CGM.Core;

namespace CGM.UI
{
    /// <summary>
    /// 血糖悬停提示触发器。
    /// 可以挂载在顶部 UI 的血糖图标区域（CGM 节点）或战斗场景底部的血糖条区域（CGM_Bar 节点）。
    /// </summary>
    public class GlucoseTooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private PlayerStats _playerStats;

        private void Start()
        {
            _playerStats = FindObjectOfType<PlayerStats>();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (TooltipManager.Instance == null) return;

            // 获取当前血糖值（若未加载出 PlayerStats，则使用默认健康值 5.7）
            float glucose = _playerStats != null ? _playerStats.CurrentGlucose : 5.7f;

            string desc = GetGlucoseStateDescription(glucose);
            TooltipManager.Instance.ShowTooltip(desc, transform as RectTransform);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (TooltipManager.Instance != null)
            {
                TooltipManager.Instance.HideTooltip(transform as RectTransform);
            }
        }

        private string GetGlucoseStateDescription(float glucose)
        {
            string rawId;
            if (glucose < BattleConstants.HealthyGlucoseMin)
                rawId = "glucose_low";
            else if (glucose <= BattleConstants.HealthyGlucoseMax)
                rawId = "glucose_healthy";
            else
                rawId = "glucose_high";

            var info = CGM.Data.BuffDatabase.GetRawTooltip(rawId);
            if (info == null) return "";

            return $"<color={info.colorHex}><b>当前状态：{info.name}</b></color>\n{info.description}";
        }
    }
}
