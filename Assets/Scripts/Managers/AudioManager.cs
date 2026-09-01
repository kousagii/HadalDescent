using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Audio Mixer")]
    [SerializeField] private AudioMixer audioMixer;

    private const string MasterVolumeParam = "MasterVolume";
    private const string BGMVolumeParam = "BGMVolume";
    private const string SFXVolumeParam = "SFXVolume";

    [Header("Audio Sources")]
    [SerializeField] private AudioSource bgmSource;
    [SerializeField] private AudioSource sfxSource;

    [Header("Menu BGM")]
    [Tooltip("Played on Splash Screen and Main Menu")]
    [SerializeField] private AudioClip mainMenuBGM;

    [Header("Zone BGM (Sunlight -> Hadal)")]
    [Tooltip("Zone 0: Sunlight Zone (0-200m)")]
    [SerializeField] private AudioClip sunlightZoneBGM;
    [Tooltip("Zone 1: Twilight Zone (200-1,000m)")]
    [SerializeField] private AudioClip twilightZoneBGM;
    [Tooltip("Zone 2: Midnight Zone (1,000-4,000m)")]
    [SerializeField] private AudioClip midnightZoneBGM;
    [Tooltip("Zone 3: Abyss Zone (4,000-6,000m)")]
    [SerializeField] private AudioClip abyssZoneBGM;
    [Tooltip("Zone 4: Hadal Zone (6,000-11,000m+)")]
    [SerializeField] private AudioClip hadalZoneBGM;

    [Header("Fallback / Generic Gameplay BGM")]
    [Tooltip("Fallback clip if a specific zone does not have a track assigned")]
    [SerializeField] private AudioClip defaultGameplayBGM;

    [Header("BGM Transition")]
    [Tooltip("Duration in seconds for smooth crossfading between music tracks (0 for instant)")]
    [SerializeField] private float fadeDuration = 1.0f;

    [Header("SFX Clips")]
    [SerializeField] private AudioClip buttonClickSFX;
    [SerializeField] private AudioClip diveSFX;
    [SerializeField] private AudioClip surfaceSFX;
    [SerializeField] private AudioClip alertSFX;

    private Coroutine _fadeCoroutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        EnsureSources();


        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        LoadAudioPreferences();

        // Auto-play music for currently active scene if not already playing
        if (bgmSource != null && !bgmSource.isPlaying)
        {
            string activeScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (activeScene == "SplashScreen" || activeScene == "MainMenu")
            {
                PlayMainMenuBGM();
            }
            else
            {
                PlayZoneBGM(ZoneManager.CurrentZoneIndex);
            }
        }
    }

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        EnsureAudioListener();
    }

    private void EnsureAudioListener()
    {
        if (FindFirstObjectByType<AudioListener>() == null)
        {
            gameObject.AddComponent<AudioListener>();
            Debug.Log("[AudioManager] Added fallback AudioListener to AudioManager GameObject.");
        }
    }

    private void EnsureSources()
    {
        if (bgmSource == null)
        {
            bgmSource = gameObject.AddComponent<AudioSource>();
            bgmSource.loop = true;
            bgmSource.playOnAwake = false;
        }

        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.loop = false;
            sfxSource.playOnAwake = false;
        }

        // Auto-route AudioSources to mixer groups if not set
        if (audioMixer != null)
        {
            if (bgmSource.outputAudioMixerGroup == null)
            {
                var groups = audioMixer.FindMatchingGroups("BGM");
                if (groups != null && groups.Length > 0)
                    bgmSource.outputAudioMixerGroup = groups[0];
            }

            if (sfxSource.outputAudioMixerGroup == null)
            {
                var groups = audioMixer.FindMatchingGroups("SFX");
                if (groups != null && groups.Length > 0)
                    sfxSource.outputAudioMixerGroup = groups[0];
            }
        }
    }

    // ── Volume Control (Slider-ready: 0.0001 - 1.0) ───────────────
    public bool  IsMuted      { get; private set; }
    public float MasterVolume { get; private set; } = 1.0f;
    public float BGMVolume    { get; private set; } = 1.0f;
    public float SFXVolume    { get; private set; } = 1.0f;

    public void LoadAudioPreferences()
    {
        MasterVolume = PlayerPrefs.GetFloat("Settings_MasterVol", 1.0f);
        BGMVolume    = PlayerPrefs.GetFloat("Settings_MusicVol", 1.0f);
        SFXVolume    = PlayerPrefs.GetFloat("Settings_SFXVol", 1.0f);
        IsMuted      = PlayerPrefs.GetInt("Settings_Muted", 0) == 1;

        SetMasterVolume(MasterVolume, false);
        SetBGMVolume(BGMVolume, false);
        SetSFXVolume(SFXVolume, false);
        SetMute(IsMuted, false);
    }

    public void SetMasterVolume(float value) => SetMasterVolume(value, true);
    public void SetMasterVolume(float value, bool save)
    {
        MasterVolume = Mathf.Clamp(value, 0.0001f, 1f);
        SetMixerVolume(MasterVolumeParam, MasterVolume);
        if (!IsMuted) AudioListener.volume = MasterVolume;
        if (save) { PlayerPrefs.SetFloat("Settings_MasterVol", MasterVolume); PlayerPrefs.Save(); }
    }

    public void SetBGMVolume(float value) => SetBGMVolume(value, true);
    public void SetBGMVolume(float value, bool save)
    {
        BGMVolume = Mathf.Clamp(value, 0.0001f, 1f);
        SetMixerVolume(BGMVolumeParam, BGMVolume);
        if (bgmSource != null && audioMixer == null) bgmSource.volume = BGMVolume;
        if (save) { PlayerPrefs.SetFloat("Settings_MusicVol", BGMVolume); PlayerPrefs.Save(); }
    }

    public void SetSFXVolume(float value) => SetSFXVolume(value, true);
    public void SetSFXVolume(float value, bool save)
    {
        SFXVolume = Mathf.Clamp(value, 0.0001f, 1f);
        SetMixerVolume(SFXVolumeParam, SFXVolume);
        if (sfxSource != null && audioMixer == null) sfxSource.volume = SFXVolume;
        if (save) { PlayerPrefs.SetFloat("Settings_SFXVol", SFXVolume); PlayerPrefs.Save(); }
    }

    public void SetMute(bool mute) => SetMute(mute, true);
    public void SetMute(bool mute, bool save)
    {
        IsMuted = mute;
        AudioListener.volume = mute ? 0f : MasterVolume;
        AudioListener.pause = mute;
        if (save) { PlayerPrefs.SetInt("Settings_Muted", mute ? 1 : 0); PlayerPrefs.Save(); }
    }

    private void SetMixerVolume(string paramName, float linearValue)
    {
        if (audioMixer == null) return;

        linearValue = Mathf.Clamp(linearValue, 0.0001f, 1f);
        audioMixer.SetFloat(paramName, Mathf.Log10(linearValue) * 20f);
    }

    // ── BGM Playback ──────────────────────────────────────────────

    /// <summary>
    /// Plays the given BGM clip. Smoothly crossfades if music is already playing.
    /// </summary>
    public void PlayBGM(AudioClip clip, bool fade = true)
    {
        if (clip == null) return;
        EnsureSources();

        // If this clip is already playing, keep playing it seamlessly
        if (bgmSource.clip == clip && bgmSource.isPlaying) return;

        if (_fadeCoroutine != null)
        {
            StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = null;
        }

        if (fade && fadeDuration > 0f && bgmSource.isPlaying)
        {
            _fadeCoroutine = StartCoroutine(FadeToNewBGM(clip, fadeDuration));
        }
        else
        {
            bgmSource.clip = clip;
            bgmSource.volume = 1f;
            bgmSource.Play();
        }
    }

    private IEnumerator FadeToNewBGM(AudioClip newClip, float duration)
    {
        float halfDuration = duration * 0.5f;
        float startVolume = bgmSource.volume > 0 ? bgmSource.volume : 1f;

        // Fade out
        for (float t = 0; t < halfDuration; t += Time.unscaledDeltaTime)
        {
            bgmSource.volume = Mathf.Lerp(startVolume, 0f, t / halfDuration);
            yield return null;
        }
        bgmSource.volume = 0f;
        bgmSource.Stop();

        // Swap clip
        bgmSource.clip = newClip;
        bgmSource.Play();

        // Fade in
        for (float t = 0; t < halfDuration; t += Time.unscaledDeltaTime)
        {
            bgmSource.volume = Mathf.Lerp(0f, startVolume, t / halfDuration);
            yield return null;
        }
        bgmSource.volume = startVolume;
        _fadeCoroutine = null;
    }

    public void StopBGM()
    {
        if (_fadeCoroutine != null)
        {
            StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = null;
        }
        if (bgmSource != null)
        {
            bgmSource.Stop();
        }
    }

    /// <summary>Plays Main Menu BGM (used on Splash Screen and Main Menu).</summary>
    public void PlayMainMenuBGM() => PlayBGM(mainMenuBGM);

    /// <summary>
    /// Plays the BGM designated for a specific zone (0 = Sunlight, 1 = Twilight, 2 = Midnight, 3 = Abyss, 4 = Hadal).
    /// If no zone-specific clip is assigned, falls back to defaultGameplayBGM.
    /// </summary>
    public void PlayZoneBGM(int zoneIndex)
    {
        AudioClip clip = GetZoneBGMClip(zoneIndex);
        if (clip != null)
        {
            PlayBGM(clip);
        }
        else if (defaultGameplayBGM != null)
        {
            PlayBGM(defaultGameplayBGM);
        }
    }

    /// <summary>
    /// Plays BGM for the currently loaded zone (retrieved from ZoneManager.CurrentZoneIndex).
    /// </summary>
    public void PlayGameplayBGM()
    {
        PlayZoneBGM(ZoneManager.CurrentZoneIndex);
    }

    public AudioClip GetZoneBGMClip(int zoneIndex)
    {
        return zoneIndex switch
        {
            0 => sunlightZoneBGM,
            1 => twilightZoneBGM,
            2 => midnightZoneBGM,
            3 => abyssZoneBGM,
            4 => hadalZoneBGM,
            _ => null
        };
    }

    // ── SFX Playback ──────────────────────────────────────────────
    public void PlaySFX(AudioClip clip)
    {
        if (clip == null) return;
        EnsureSources();
        sfxSource.PlayOneShot(clip);
    }

    public void PlayButtonClick() => PlaySFX(buttonClickSFX);
    public void PlayDive() => PlaySFX(diveSFX);
    public void PlaySurface() => PlaySFX(surfaceSFX);
    public void PlayAlert() => PlaySFX(alertSFX);
}