// =============================================================================
// GlucoseBarUI.cs — 血糖条 UI 控制器
// 命名空间：CGM.UI
// 职责：配合 PlayerStats，实时渲染血糖条滑块位置和数值文本。
//       挂载在 CGM_Bar 上。
// =============================================================================

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CGM.Core;

namespace CGM.UI
{
    public class GlucoseBarUI : MonoBehaviour
    {
        [Header("血糖条")]
        [SerializeField] private Slider glucoseSlider;
        [SerializeField] private TextMeshProUGUI glucoseValueText;

        private PlayerStats _playerStats;

        private void Start()
        {
            _playerStats = FindObjectOfType<PlayerStats>();
            if (_playerStats != null)
            {
                _playerStats.OnGlucoseChanged += RefreshUI;
                RefreshUI(_playerStats.CurrentGlucose);
            }

            // 血糖条只显示，不响应拖拽
            if (glucoseSlider != null) glucoseSlider.interactable = false;
        }

        private void OnDestroy()
        {
            if (_playerStats != null)
            {
                _playerStats.OnGlucoseChanged -= RefreshUI;
            }
        }

        private void RefreshUI(float glucose)
        {
            if (glucoseSlider != null)
            {
                glucoseSlider.value = Mathf.Clamp(glucose, glucoseSlider.minValue, glucoseSlider.maxValue);
            }
            if (glucoseValueText != null)
            {
                string colorHex = GetGlucoseStateColorHex(glucose);
                glucoseValueText.text = $"<color={colorHex}>{glucose:F1}</color>";
            }
        }

        private string GetGlucoseStateColorHex(float glucose)
        {
            if (glucose < BattleConstants.HealthyGlucoseMin) return BattleConstants.ColorOrange;
            if (glucose <= BattleConstants.HealthyGlucoseMax) return BattleConstants.ColorGreen;
            return BattleConstants.ColorRed;
        }
    }
}
