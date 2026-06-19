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
                TooltipManager.Instance.HideTooltip();
            }
        }

        private string GetGlucoseStateDescription(float glucose)
        {
            if (glucose < BattleConstants.HealthyGlucoseMin)
            {
                var info = CGM.Data.BuffDatabase.GetRawTooltip("glucose_low");
                if (info == null) return "";
                string descBody = string.Format(info.description, glucose, BattleConstants.HealthyGlucoseMin, BattleConstants.GlucoseDeathMin);
                return $"<color={BattleConstants.ColorOrange}><b>当前状态：{info.name}</b></color>\n" + descBody;
            }
            else if (glucose <= BattleConstants.HealthyGlucoseMax)
            {
                var info = CGM.Data.BuffDatabase.GetRawTooltip("glucose_healthy");
                if (info == null) return "";
                string descBody = string.Format(info.description, glucose, BattleConstants.HealthyGlucoseMin, BattleConstants.HealthyGlucoseMax);
                return $"<color={BattleConstants.ColorGreen}><b>当前状态：{info.name}</b></color>\n" + descBody;
            }
            else
            {
                var info = CGM.Data.BuffDatabase.GetRawTooltip("glucose_high");
                if (info == null) return "";
                string descBody = string.Format(info.description, glucose, BattleConstants.HealthyGlucoseMax, BattleConstants.GlucoseDeathMax);
                return $"<color={BattleConstants.ColorRed}><b>当前状态：{info.name}</b></color>\n" + descBody;
            }
        }
    }
}
