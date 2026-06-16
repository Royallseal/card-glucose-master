// =============================================================================
// PlayerUI.cs — 玩家 UI 表现层控制器
// 命名空间：CGM.UI
// 职责：配合 PlayerStats，实时渲染血量、格挡、Buff、血糖状态等。
//       挂载在 Player_Stat 上。
// =============================================================================

using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CGM.Core;
using CGM.Data;

namespace CGM.UI
{
    public class PlayerUI : MonoBehaviour
    {
        [Header("基础 UI 引用")]
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private Slider hpSlider;
        [SerializeField] private TextMeshProUGUI hpText;

        [Header("格挡 UI 引用")]
        [SerializeField] private GameObject blockContainer;
        [SerializeField] private TextMeshProUGUI blockText;

        [Header("目标锁定指示器")]
        [SerializeField] private GameObject targetIndicator;

        [Header("Buff/Debuff UI 容器")]
        [SerializeField] private Transform buffContainer;
        [SerializeField] private GameObject buffIconPrefab;

        private PlayerStats _playerStats;

        private void Start()
        {
            if (_playerStats == null)
            {
                _playerStats = GetComponent<PlayerStats>();
                if (_playerStats == null)
                    _playerStats = GetComponentInParent<PlayerStats>();
            }
            if (_playerStats != null)
            {
                _playerStats.OnStatsChanged += RefreshUI;
                RefreshUI();
            }
        }

        private void OnDestroy()
        {
            if (_playerStats != null)
            {
                _playerStats.OnStatsChanged -= RefreshUI;
            }
        }

        public void RefreshUI()
        {
            if (_playerStats == null) return;

            // 名字
            if (nameText != null)
            {
                nameText.text = "控糖师";
            }

            // 血量条 + 文本
            if (hpSlider != null)
            {
                hpSlider.maxValue = _playerStats.MaxHp;
                hpSlider.value = _playerStats.CurrentHp;
            }
            if (hpText != null)
            {
                hpText.text = $"{_playerStats.CurrentHp}/{_playerStats.MaxHp}";
            }

            // 格挡
            int block = _playerStats.Block;
            if (blockContainer != null)
            {
                blockContainer.SetActive(block > 0);
            }
            if (blockText != null && block > 0)
            {
                blockText.text = block.ToString();
            }

            // Buff 图标
            RefreshBuffIcons();
        }

        private void RefreshBuffIcons()
        {
            if (buffContainer == null || buffIconPrefab == null) return;

            foreach (Transform child in buffContainer)
            {
                Destroy(child.gameObject);
            }

            var activeBuffs = _playerStats.GetAllActiveBuffs();
            foreach (var kvp in activeBuffs)
            {
                BuffId id = kvp.Key;
                int stacks = kvp.Value;
                if (stacks == 0) continue;

                GameObject iconGo = Instantiate(buffIconPrefab, buffContainer);
                iconGo.name = $"Buff_{id}";

                Image img = iconGo.GetComponent<Image>();
                TextMeshProUGUI txt = iconGo.GetComponentInChildren<TextMeshProUGUI>();

                string spritePath = BuffDatabase.GetSpritePath(id);
                Sprite sp = Resources.Load<Sprite>(spritePath);
                if (img != null && sp != null) img.sprite = sp;

                // 应用状态对应颜色
                if (img != null)
                {
                    var info = BuffDatabase.Get(id);
                    if (info != null && ColorUtility.TryParseHtmlString(info.colorHex, out Color c))
                        img.color = c;
                    else
                        img.color = Color.white;
                }

                if (txt != null) txt.text = stacks.ToString();
            }
        }

    }
}
