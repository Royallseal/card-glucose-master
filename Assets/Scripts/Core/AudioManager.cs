// =============================================================================
// AudioManager.cs — 全局音量管理器
// 命名空间：CGM.Core
// 职责：统一管理 BGM 音量与 SFX 音量，通过 PlayerPrefs 持久化，
//       为 BgmManager 和其他 SFX 播放提供实时音量控制。
// =============================================================================

using UnityEngine;

namespace CGM.Core
{
    /// <summary>
    /// 全局音频管理器单例。管理背景音乐和音效的实时音量，
    /// 并与 PlayerPrefs 双向同步以保证退出游戏后设置不丢失。
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        private const string PrefKeyBgm = "CGM_BgmVolume";
        private const string PrefKeySfx = "CGM_SfxVolume";
        private const float DefaultVolume = 0.80f;

        /// <summary>
        /// BGM 音量 (0~1)，内部存储。
        /// </summary>
        private float _bgmVolume = DefaultVolume;

        /// <summary>
        /// SFX 音量 (0~1)，内部存储。
        /// </summary>
        private float _sfxVolume = DefaultVolume;

        /// <summary>
        /// 当前 BGM 音量 (0~1)。
        /// </summary>
        public float BgmVolume => _bgmVolume;

        /// <summary>
        /// 当前 SFX 音量 (0~1)。
        /// </summary>
        public float SfxVolume => _sfxVolume;

        /// <summary>
        /// BGM 音量发生变化时触发。
        /// </summary>
        public event System.Action<float> OnBgmVolumeChanged;

        /// <summary>
        /// SFX 音量发生变化时触发。
        /// </summary>
        public event System.Action<float> OnSfxVolumeChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            LoadVolumes();
        }

        /// <summary>
        /// 设置 BGM 音量（内部 0~1 范围）。
        /// </summary>
        public void SetBgmVolume(float volume01)
        {
            float clamped = Mathf.Clamp01(volume01);
            if (Mathf.Approximately(_bgmVolume, clamped)) return;

            _bgmVolume = clamped;
            PlayerPrefs.SetFloat(PrefKeyBgm, clamped);
            PlayerPrefs.Save();
            OnBgmVolumeChanged?.Invoke(clamped);
        }

        /// <summary>
        /// 设置 SFX 音量（内部 0~1 范围）。
        /// </summary>
        public void SetSfxVolume(float volume01)
        {
            float clamped = Mathf.Clamp01(volume01);
            if (Mathf.Approximately(_sfxVolume, clamped)) return;

            _sfxVolume = clamped;
            PlayerPrefs.SetFloat(PrefKeySfx, clamped);
            PlayerPrefs.Save();
            OnSfxVolumeChanged?.Invoke(clamped);
        }

        /// <summary>
        /// 将 Slider 的原始值（如 0~100）映射为 0~1 音量并设置。
        /// </summary>
        public void SetBgmVolumeFromSlider(float sliderValue, float sliderMin, float sliderMax)
        {
            float t = Mathf.InverseLerp(sliderMin, sliderMax, sliderValue);
            SetBgmVolume(t);
        }

        /// <summary>
        /// 将 Slider 的原始值（如 0~100）映射为 0~1 音量并设置。
        /// </summary>
        public void SetSfxVolumeFromSlider(float sliderValue, float sliderMin, float sliderMax)
        {
            float t = Mathf.InverseLerp(sliderMin, sliderMax, sliderValue);
            SetSfxVolume(t);
        }

        /// <summary>
        /// 将 0~1 音量值映射回 Slider 的原始范围。
        /// </summary>
        public static float VolumeToSlider(float volume01, float sliderMin, float sliderMax)
        {
            return Mathf.Lerp(sliderMin, sliderMax, volume01);
        }

        /// <summary>
        /// 播放一个音效片段，自动按当前 SFX 音量缩放。
        /// </summary>
        public void PlaySfx(AudioClip clip, Vector3 position)
        {
            if (clip == null) return;
            AudioSource.PlayClipAtPoint(clip, position, _sfxVolume);
        }

        /// <summary>
        /// 播放一个音效片段，在全局 SFX 音量基础上再乘以倍率。
        /// 用于个别音效需要比全局音量更大/更小时。
        /// </summary>
        public void PlaySfx(AudioClip clip, Vector3 position, float volumeMultiplier)
        {
            if (clip == null) return;
            AudioSource.PlayClipAtPoint(clip, position, _sfxVolume * volumeMultiplier);
        }

        /// <summary>
        /// 播放一个音效片段（静态便捷方法）。
        /// </summary>
        public static void PlaySfxStatic(AudioClip clip, Vector3 position)
        {
            if (Instance != null)
            {
                Instance.PlaySfx(clip, position);
            }
            else
            {
                // 降级：AudioManager 尚未初始化时用默认音量播放
                AudioSource.PlayClipAtPoint(clip, position, DefaultVolume);
            }
        }

        /// <summary>
        /// 播放一个音效片段（静态便捷方法，带音量倍率）。
        /// </summary>
        public static void PlaySfxStatic(AudioClip clip, Vector3 position, float volumeMultiplier)
        {
            if (Instance != null)
            {
                Instance.PlaySfx(clip, position, volumeMultiplier);
            }
            else
            {
                AudioSource.PlayClipAtPoint(clip, position, DefaultVolume * volumeMultiplier);
            }
        }

        private void LoadVolumes()
        {
            _bgmVolume = PlayerPrefs.GetFloat(PrefKeyBgm, DefaultVolume);
            _sfxVolume = PlayerPrefs.GetFloat(PrefKeySfx, DefaultVolume);
        }
    }
}
