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
            // 组合并高亮展示血糖说明
            if (glucose < BattleConstants.HealthyGlucoseMin)
            {
                return $"<color={BattleConstants.ColorOrange}><b>当前状态：低血糖</b></color>\n" +
                       $"血糖值：{glucose:F1} (< {BattleConstants.HealthyGlucoseMin:F1})\n\n" +
                       $"无伤害与格挡修正。若血糖低于 <color={BattleConstants.ColorRed}><b>{BattleConstants.GlucoseDeathMin:F1}</b></color>，将因血糖缺失而立即死亡！";
            }
            else if (glucose <= BattleConstants.HealthyGlucoseMax)
            {
                return $"<color={BattleConstants.ColorGreen}><b>当前状态：血糖健康</b></color>\n" +
                       $"血糖值：{glucose:F1} ({BattleConstants.HealthyGlucoseMin:F1} - {BattleConstants.HealthyGlucoseMax:F1})\n\n" +
                       $"<color={BattleConstants.ColorGreen}><b>打出的所有卡牌伤害与格挡值 +25%</b></color>。";
            }
            else
            {
                return $"<color={BattleConstants.ColorRed}><b>当前状态：高血糖</b></color>\n" +
                       $"血糖值：{glucose:F1} (> {BattleConstants.HealthyGlucoseMax:F1})\n\n" +
                       $"打出的所有卡牌伤害与格挡值 <color={BattleConstants.ColorRed}><b>-25%</b></color>，且卡牌带来的血糖变化幅度 <color={BattleConstants.ColorOrange}><b>翻倍 (x2.0)</b></color>！\n" +
                       $"若血糖高于 <color={BattleConstants.ColorRed}><b>{BattleConstants.GlucoseDeathMax:F1}</b></color>，将因高血糖危象而立即死亡！";
            }
        }
    }
}
