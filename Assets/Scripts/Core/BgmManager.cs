using System.Collections;
using UnityEngine;

namespace CGM.Core
{
    /// <summary>
    /// 全局背景音乐管理器，负责音乐的加载、循环播放以及淡入淡出平滑过渡。
    /// </summary>
    public class BgmManager : MonoBehaviour
    {
        public static BgmManager Instance { get; private set; }

        [Header("淡入淡出配置")]
        [SerializeField] private float defaultFadeDuration = 1.5f;  // 默认过渡时长
        [SerializeField] private float targetVolume = 0.55f;        // 目标音量（0~1）

        private AudioSource audioSource;
        private Coroutine fadeCoroutine;
        private string currentTrackName = "";
        private bool hasDelayedStartDone = false;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }

            audioSource.loop = true;
            audioSource.playOnAwake = false;
            audioSource.volume = 0f; // 初始静音，靠淡入控制
        }

        /// <summary>
        /// 播放指定的背景音乐（带淡入淡出过渡）。
        /// </summary>
        /// <param name="trackName">音乐轨道名称</param>
        /// <param name="fadeDuration">淡入淡出过渡时长（负数则使用默认值）</param>
        public void PlayBgm(string trackName, float fadeDuration = -1f)
        {
            if (fadeDuration < 0f) fadeDuration = defaultFadeDuration;

            if (string.IsNullOrEmpty(trackName))
            {
                StopBgm(fadeDuration);
                return;
            }

            if (currentTrackName.ToLower() == trackName.ToLower() && audioSource.isPlaying)
            {
                return; // 如果已经是同一首曲子且在播放，直接返回
            }

            currentTrackName = trackName;
            string resourcePath = GetResourcePath(trackName);
            AudioClip clip = Resources.Load<AudioClip>(resourcePath);

            if (clip == null)
            {
                Debug.LogWarning($"[BgmManager] 未能从 Resources 加载音乐: {resourcePath}，参数: {trackName}");
                StopBgm(fadeDuration);
                return;
            }

            if (fadeCoroutine != null)
            {
                StopCoroutine(fadeCoroutine);
            }
            fadeCoroutine = StartCoroutine(TransitionToBgm(clip, fadeDuration, !hasDelayedStartDone));
        }

        /// <summary>
        /// 停止播放当前背景音乐（带淡出）。
        /// </summary>
        public void StopBgm(float fadeDuration = -1f)
        {
            if (fadeDuration < 0f) fadeDuration = defaultFadeDuration;

            currentTrackName = "";
            if (fadeCoroutine != null)
            {
                StopCoroutine(fadeCoroutine);
            }
            fadeCoroutine = StartCoroutine(FadeOutRoutine(fadeDuration));
        }

        private IEnumerator TransitionToBgm(AudioClip newClip, float duration, bool isInitialPlay)
        {
            // 首次播放时延迟一小段时间，避免在场景刚加载/编译完成瞬间就开始播放
            if (isInitialPlay)
            {
                yield return new WaitForSeconds(0.3f);
                hasDelayedStartDone = true;
            }

            // 1. 如果当前有音乐在播放，先淡出
            if (audioSource.isPlaying && audioSource.volume > 0f)
            {
                float startVol = audioSource.volume;
                float elapsed = 0f;
                float halfDuration = duration * 0.5f;
                while (elapsed < halfDuration)
                {
                    elapsed += Time.deltaTime;
                    audioSource.volume = Mathf.Lerp(startVol, 0f, elapsed / halfDuration);
                    yield return null;
                }
            }

            audioSource.Stop();
            audioSource.clip = newClip;
            audioSource.volume = 0f;
            audioSource.Play();

            // 2. 淡入新音乐
            {
                float elapsed = 0f;
                float halfDuration = duration * 0.5f;
                while (elapsed < halfDuration)
                {
                    elapsed += Time.deltaTime;
                    audioSource.volume = Mathf.Lerp(0f, targetVolume, elapsed / halfDuration);
                    yield return null;
                }
                audioSource.volume = targetVolume;
            }

            fadeCoroutine = null;
        }

        private IEnumerator FadeOutRoutine(float duration)
        {
            if (audioSource.isPlaying)
            {
                float startVol = audioSource.volume;
                float elapsed = 0f;
                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    audioSource.volume = Mathf.Lerp(startVol, 0f, elapsed / duration);
                    yield return null;
                }
                audioSource.Stop();
                audioSource.volume = 0f;
            }
            fadeCoroutine = null;
        }

        private string GetResourcePath(string trackName)
        {
            switch (trackName.ToLower())
            {
                case "dance of fireflies":
                    return "Audio/Music/Dance Of The Fireflies - Philter";
                case "battle scars":
                    return "Audio/Music/Battle Scars - Philter";
                case "advent time":
                case "adventure time":
                    return "Audio/Music/Adventure Time - Philter";
                case "back to yesterday":
                    return "Audio/Music/Back To Yesterday - Philter";
                default:
                    return trackName; // 备用直接路径
            }
        }
    }
}
