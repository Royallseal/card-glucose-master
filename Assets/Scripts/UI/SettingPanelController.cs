// =============================================================================
// SettingPanelController.cs — 设置面板控制器
// 命名空间：CGM.UI
// 职责：管理设置界面的音量滑块、卡牌图鉴入口、返回主菜单确认等逻辑。
//       支持多入口（开始界面 / 游戏中顶部 UI），自动管理半透明遮罩层级。
// =============================================================================

using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using CGM.Core;

namespace CGM.UI
{
    /// <summary>
    /// 设置面板总控脚本，挂载在 SettingPanel 根节点上。
    /// 管理音量调节、卡牌图鉴跳转、返回主菜单确认退出等。
    /// </summary>
    public class SettingPanelController : MonoBehaviour
    {
        [Header("基本设置区域")]
        [SerializeField] private GameObject settingArea;

        [Header("确认退出区域")]
        [SerializeField] private GameObject confirmToExitPanel;

        [Header("音量滑块")]
        [SerializeField] private Slider bgmSlider;
        [SerializeField] private Slider sfxSlider;

        [Header("按钮")]
        [SerializeField] private Button backButton;           // SettingArea/ExitUI
        [SerializeField] private Button cardLibraryButton;    // SettingArea/SettingDetail/Cards
        [SerializeField] private Button exitToStartingButton; // SettingArea/SettingDetail/ExitToStarting
        [SerializeField] private Button confirmBackButton;    // ConfirmToExit/ExitUI
        [SerializeField] private Button confirmOkButton;      // ConfirmToExit/ConfirmUI

        [Header("标题")]
        [SerializeField] private TextMeshProUGUI titleText;

        [Header("动画参数")]
        [SerializeField] private float animDuration = 0.25f;

        // 运行时状态
        private GameObject sourcePanel;         // 进入设置前激活的面板
        private bool cameFromStartingPanel;     // 是否从开始界面进入
        private bool isOpen = false;
        private Coroutine animCoroutine;

        private void Awake()
        {
            AutoResolveReferences();
        }

        private void OnEnable()
        {
            // 首次打开时播放入场动画
            if (isOpen && animCoroutine == null)
            {
                PlayOpenAnimation();
            }
        }

        private void Start()
        {
            BindButtonEvents();
        }

        private void OnDestroy()
        {
            UnbindButtonEvents();
        }

        // =====================================================================
        // 公开接口
        // =====================================================================

        /// <summary>
        /// 打开设置面板。
        /// </summary>
        /// <param name="srcPanel">来源面板（将被半透明遮罩覆盖，不会被隐藏）</param>
        /// <param name="fromStartingPanel">是否从开始界面进入</param>
        public void Open(GameObject srcPanel, bool fromStartingPanel)
        {
            if (isOpen) return;

            sourcePanel = srcPanel;
            cameFromStartingPanel = fromStartingPanel;
            isOpen = true;

            // 设置面板覆盖在来源面板上方（来源面板保持激活，透过半透明 Dark_Mask 可见）
            gameObject.SetActive(true);

            // 确保基本设置界面显示，确认退出界面隐藏
            if (settingArea != null) settingArea.SetActive(true);
            if (confirmToExitPanel != null) confirmToExitPanel.SetActive(false);

            // 根据来源决定是否显示"返回主菜单"按钮
            if (exitToStartingButton != null)
            {
                exitToStartingButton.gameObject.SetActive(!cameFromStartingPanel);
            }

            // 同步音量滑块到 AudioManager 当前值
            SyncSlidersFromAudioManager();

            // 播放入场动画
            PlayOpenAnimation();
        }

        /// <summary>
        /// 关闭设置面板，恢复来源面板。
        /// </summary>
        public void Close()
        {
            if (!isOpen) return;

            // 如果确认退出界面正打开，先回到基本设置
            if (confirmToExitPanel != null && confirmToExitPanel.activeSelf)
            {
                confirmToExitPanel.SetActive(false);
                if (settingArea != null) settingArea.SetActive(true);
                return;
            }

            isOpen = false;

            // 播放出场动画后关闭
            if (animCoroutine != null) StopCoroutine(animCoroutine);
            animCoroutine = StartCoroutine(CloseAnimationRoutine());
        }

        /// <summary>
        /// 确认返回主菜单（由 ConfirmToExit 的确认按钮触发）。
        /// 关闭设置面板，跳转到开始界面。
        /// </summary>
        public void ConfirmReturnToMainMenu()
        {
            isOpen = false;
            gameObject.SetActive(false);

            // 通知 GameSessionManager 返回主菜单
            var gsm = GameSessionManager.Instance;
            if (gsm != null)
            {
                gsm.ReturnToMainMenu();
            }
        }

        // =====================================================================
        // 按钮回调
        // =====================================================================

        private void OnBackClicked()
        {
            Close();
        }

        private void OnCardLibraryClicked()
        {
            // 打开卡牌图鉴（复用 CardsMapPanel，与抽牌堆/弃牌堆一致）
            var gsm = GameSessionManager.Instance;
            if (gsm != null)
            {
                // 以设置面板自身为源面板；CardLibrary 关闭时会恢复设置面板
                gsm.OpenCardsMapFromSettings(CardsMapMode.CardLibrary, gameObject);
            }
        }

        private void OnExitToStartingClicked()
        {
            // 显示确认退出二级菜单
            if (settingArea != null) settingArea.SetActive(false);
            if (confirmToExitPanel != null) confirmToExitPanel.SetActive(true);
        }

        private void OnConfirmBackClicked()
        {
            // 从确认退出回到基本设置
            if (confirmToExitPanel != null) confirmToExitPanel.SetActive(false);
            if (settingArea != null) settingArea.SetActive(true);
        }

        private void OnConfirmOkClicked()
        {
            ConfirmReturnToMainMenu();
        }

        // =====================================================================
        // 音量滑块同步
        // =====================================================================

        private void OnBgmSliderChanged(float value)
        {
            if (bgmSlider != null && AudioManager.Instance != null)
            {
                AudioManager.Instance.SetBgmVolumeFromSlider(
                    value, bgmSlider.minValue, bgmSlider.maxValue);
            }
        }

        private void OnSfxSliderChanged(float value)
        {
            if (sfxSlider != null && AudioManager.Instance != null)
            {
                AudioManager.Instance.SetSfxVolumeFromSlider(
                    value, sfxSlider.minValue, sfxSlider.maxValue);
            }
        }

        private void SyncSlidersFromAudioManager()
        {
            if (AudioManager.Instance == null) return;

            if (bgmSlider != null)
            {
                float sliderVal = AudioManager.VolumeToSlider(
                    AudioManager.Instance.BgmVolume,
                    bgmSlider.minValue, bgmSlider.maxValue);
                bgmSlider.SetValueWithoutNotify(sliderVal);
            }

            if (sfxSlider != null)
            {
                float sliderVal = AudioManager.VolumeToSlider(
                    AudioManager.Instance.SfxVolume,
                    sfxSlider.minValue, sfxSlider.maxValue);
                sfxSlider.SetValueWithoutNotify(sliderVal);
            }
        }

        // =====================================================================
        // 入场/出场动画
        // =====================================================================

        private void PlayOpenAnimation()
        {
            if (animCoroutine != null) StopCoroutine(animCoroutine);
            animCoroutine = StartCoroutine(OpenAnimationRoutine());
        }

        private IEnumerator OpenAnimationRoutine()
        {
            CanvasGroup cg = GetComponent<CanvasGroup>();
            if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            cg.interactable = false;

            float elapsed = 0f;
            while (elapsed < animDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / animDuration);
                // Ease-out
                float eased = 1f - (1f - t) * (1f - t);
                cg.alpha = eased;
                yield return null;
            }
            cg.alpha = 1f;
            cg.interactable = true;
            animCoroutine = null;
        }

        private IEnumerator CloseAnimationRoutine()
        {
            CanvasGroup cg = GetComponent<CanvasGroup>();
            if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();
            cg.interactable = false;

            float startAlpha = cg.alpha;
            float elapsed = 0f;
            while (elapsed < animDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / animDuration);
                cg.alpha = Mathf.Lerp(startAlpha, 0f, t);
                yield return null;
            }
            cg.alpha = 0f;
            gameObject.SetActive(false);
            animCoroutine = null;
        }

        // =====================================================================
        // 自动解析引用
        // =====================================================================

        private void AutoResolveReferences()
        {
            // 基本设置区域
            if (settingArea == null)
            {
                Transform t = transform.Find("SettingArea");
                if (t != null) settingArea = t.gameObject;
            }

            // 确认退出区域
            if (confirmToExitPanel == null)
            {
                Transform t = transform.Find("ConfirmToExit");
                if (t != null) confirmToExitPanel = t.gameObject;
            }

            // 标题
            if (titleText == null)
            {
                Transform t = transform.Find("SettingArea/TitleUI/TitleText");
                if (t != null) titleText = t.GetComponent<TextMeshProUGUI>();
            }

            // 音量滑块
            if (bgmSlider == null)
            {
                Transform t = transform.Find("SettingArea/SettingDetail/AudioSetting/Music_Row/Music_Slider");
                if (t != null) bgmSlider = t.GetComponent<Slider>();
            }
            if (sfxSlider == null)
            {
                Transform t = transform.Find("SettingArea/SettingDetail/AudioSetting/SFX_Row/SFX_Slider");
                if (t != null) sfxSlider = t.GetComponent<Slider>();
            }

            // 基本设置按钮
            if (backButton == null)
            {
                Transform t = transform.Find("SettingArea/ExitUI");
                if (t != null) backButton = t.GetComponent<Button>();
            }
            if (cardLibraryButton == null)
            {
                Transform t = transform.Find("SettingArea/SettingDetail/Cards");
                if (t != null) cardLibraryButton = t.GetComponent<Button>();
            }
            if (exitToStartingButton == null)
            {
                Transform t = transform.Find("SettingArea/SettingDetail/ExitToStarting");
                if (t != null) exitToStartingButton = t.GetComponent<Button>();
            }

            // 确认退出按钮
            if (confirmBackButton == null)
            {
                Transform t = transform.Find("ConfirmToExit/ExitUI");
                if (t != null) confirmBackButton = t.GetComponent<Button>();
            }
            if (confirmOkButton == null)
            {
                Transform t = transform.Find("ConfirmToExit/ConfirmUI");
                if (t != null) confirmOkButton = t.GetComponent<Button>();
            }
        }

        // =====================================================================
        // 事件绑定/解绑
        // =====================================================================

        private void BindButtonEvents()
        {
            if (backButton != null)
            {
                backButton.onClick.RemoveAllListeners();
                backButton.onClick.AddListener(OnBackClicked);
                AddHoverEffect(backButton);
            }

            if (cardLibraryButton != null)
            {
                cardLibraryButton.onClick.RemoveAllListeners();
                cardLibraryButton.onClick.AddListener(OnCardLibraryClicked);
                AddHoverEffect(cardLibraryButton);
            }

            if (exitToStartingButton != null)
            {
                exitToStartingButton.onClick.RemoveAllListeners();
                exitToStartingButton.onClick.AddListener(OnExitToStartingClicked);
                AddHoverEffect(exitToStartingButton);
            }

            if (confirmBackButton != null)
            {
                confirmBackButton.onClick.RemoveAllListeners();
                confirmBackButton.onClick.AddListener(OnConfirmBackClicked);
                AddHoverEffect(confirmBackButton);
            }

            if (confirmOkButton != null)
            {
                confirmOkButton.onClick.RemoveAllListeners();
                confirmOkButton.onClick.AddListener(OnConfirmOkClicked);
                AddHoverEffect(confirmOkButton);
            }

            if (bgmSlider != null)
            {
                bgmSlider.onValueChanged.RemoveAllListeners();
                bgmSlider.onValueChanged.AddListener(OnBgmSliderChanged);
            }

            if (sfxSlider != null)
            {
                sfxSlider.onValueChanged.RemoveAllListeners();
                sfxSlider.onValueChanged.AddListener(OnSfxSliderChanged);
            }
        }

        private void UnbindButtonEvents()
        {
            if (backButton != null) backButton.onClick.RemoveListener(OnBackClicked);
            if (cardLibraryButton != null) cardLibraryButton.onClick.RemoveListener(OnCardLibraryClicked);
            if (exitToStartingButton != null) exitToStartingButton.onClick.RemoveListener(OnExitToStartingClicked);
            if (confirmBackButton != null) confirmBackButton.onClick.RemoveListener(OnConfirmBackClicked);
            if (confirmOkButton != null) confirmOkButton.onClick.RemoveListener(OnConfirmOkClicked);
            if (bgmSlider != null) bgmSlider.onValueChanged.RemoveListener(OnBgmSliderChanged);
            if (sfxSlider != null) sfxSlider.onValueChanged.RemoveListener(OnSfxSliderChanged);
        }

        private void AddHoverEffect(Button btn)
        {
            var hover = btn.gameObject.GetComponent<UIHoverButtonEffects>();
            if (hover == null) hover = btn.gameObject.AddComponent<UIHoverButtonEffects>();
            AudioClip clip = Resources.Load<AudioClip>("Audio/Button_Hover");
            hover.Setup(clip, 1.0f);
        }
    }
}
