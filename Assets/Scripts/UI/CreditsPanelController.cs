using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CGM.UI
{
    /// <summary>
    /// Credits / 版权署名面板控制器。
    /// 显示第三方素材的作者署名和许可证信息。
    /// </summary>
    public class CreditsPanelController : MonoBehaviour
    {
        [Header("UI 引用")]
        [SerializeField] private GameObject creditsPanel;
        [SerializeField] private Button closeButton;
        [SerializeField] private TextMeshProUGUI creditsText;

        private void Start()
        {
            if (closeButton != null)
                closeButton.onClick.AddListener(Hide);

            // 默认隐藏
            if (creditsPanel != null)
                creditsPanel.SetActive(false);

            // 设置署名文本
            if (creditsText != null)
                creditsText.text = GetCreditsText();
        }

        /// <summary>
        /// 显示 Credits 面板。
        /// </summary>
        public void Show()
        {
            if (creditsPanel != null)
                creditsPanel.SetActive(true);
        }

        /// <summary>
        /// 隐藏 Credits 面板。
        /// </summary>
        public void Hide()
        {
            if (creditsPanel != null)
                creditsPanel.SetActive(false);
        }

        /// <summary>
        /// 切换显示/隐藏。
        /// </summary>
        public void Toggle()
        {
            if (creditsPanel != null)
                creditsPanel.SetActive(!creditsPanel.activeSelf);
        }

        private static string GetCreditsText()
        {
            return @"<b>—— 美术素材 ——</b>

<b>卡牌 UI 素材</b>
UI Pack by Kenney (kenney.nl)
Licensed under CC0 1.0

<b>卡牌模板</b>
Mechanized Magic Card Template by Dumivid (dumivid.itch.io)
Licensed under CC0 1.0

<b>图标</b>
Icons by Lorc, Delapouite, Caro Asercion, Sbed, Rihlsul, Skoll, Faithtoken, Quoting, Seregacthtuf
from game-icons.net
Licensed under CC BY 3.0

<b>背景</b>
Background images generated with Magnific AI (magnific.com)

<b>中文字体</b>
芫茜雅楷 (JyunsaiKaai) by Mark Li · SIL OFL 1.1
玄宗体 (XuanZongTi) by Yuchen Tian · SIL OFL 1.1
仓耳非白 (Logo) · 商免字体

<i>—— 音效素材 ——</i>

<b>界面 & 战斗音效</b>
Interface Sounds, UI Audio, UI Pack Sounds
by Kenney (kenney.nl)
Licensed under CC0 1.0

<i>—— 音乐素材 ——</i>

<b>背景音乐</b>
""16-bit Fantasy & Adventure Music Pack""
Original music by Marllon Silva (xDeviruchi)
https://xdeviruchi.itch.io/16-bit-fantasy-adventure-music-pack

<i>—— 开发工具 ——</i>

Made with Unity
TextMesh Pro by Unity Technologies";
        }
    }
}
