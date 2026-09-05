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

    [Header("SFX Clips - UI & Navigation")]
    [Tooltip("Played when buttons are clicked (Shop, Bestiary, Pause, etc.)")]
    [SerializeField] private AudioClip buttonClickSFX;
    [Tooltip("Played when clicking the Scan/Camera button")]
    [SerializeField] private AudioClip cameraShutterSFX;

    [Header("SFX Clips - Detection & Scanning")]
    [Tooltip("Played when the reticle targets and highlights a debris cluster")]
    [SerializeField] private AudioClip debrisFoundSFX;
    [Tooltip("Played when the reticle targets and highlights a species")]
    [SerializeField] private AudioClip speciesFoundSFX;

    [Header("SFX Clips - Submarine & Movement")]
    [Tooltip("Looping engine / propeller / thruster sound when submarine is moving")]
    [SerializeField] private AudioClip subMovementSFX;
    [SerializeField] private AudioSource subMovementSource;

    [Header("SFX Clips - Transitions & World")]
    [Tooltip("Transition sound from Zone Selection into gameplay")]
    [SerializeField] private AudioClip zoneTransitionSFX;
    [Tooltip("How long in seconds the zone transition sound will play before stopping (default: 3.0s)")]
    [SerializeField] private float zoneTransitionDuration = 3.0f;
    [Tooltip("Duration in seconds for smooth fade-out at the end of the transition sound (default: 0.5s)")]
    [SerializeField] private float zoneTransitionFadeOut = 0.5f;
    [SerializeField] private AudioSource transitionSource;
    [SerializeField] private AudioClip diveSFX;
    [SerializeField] private AudioClip surfaceSFX;
    [SerializeField] private AudioClip alertSFX;

    private Coroutine _fadeCoroutine;
    private Coroutine _transitionCoroutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Instance.CopyMissingClips(this);
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        EnsureSources();

        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    public void CopyMissingClips(AudioManager other)
    {
        if (other == null) return;
        if (buttonClickSFX == null)     buttonClickSFX     = other.buttonClickSFX;
        if (cameraShutterSFX == null)   cameraShutterSFX   = other.cameraShutterSFX;
        if (debrisFoundSFX == null)     debrisFoundSFX     = other.debrisFoundSFX;
        if (speciesFoundSFX == null)    speciesFoundSFX    = other.speciesFoundSFX;
        if (subMovementSFX == null)     subMovementSFX     = other.subMovementSFX;
        if (zoneTransitionSFX == null)
        {
            zoneTransitionSFX      = other.zoneTransitionSFX;
            zoneTransitionDuration = other.zoneTransitionDuration;
            zoneTransitionFadeOut  = other.zoneTransitionFadeOut;
        }
        if (sunlightZoneBGM == null)    sunlightZoneBGM    = other.sunlightZoneBGM;
        if (twilightZoneBGM == null)    twilightZoneBGM    = other.twilightZoneBGM;
        if (midnightZoneBGM == null)    midnightZoneBGM    = other.midnightZoneBGM;
        if (abyssZoneBGM == null)       abyssZoneBGM       = other.abyssZoneBGM;
        if (hadalZoneBGM == null)       hadalZoneBGM       = other.hadalZoneBGM;
        if (mainMenuBGM == null)        mainMenuBGM        = other.mainMenuBGM;
        if (defaultGameplayBGM == null) defaultGameplayBGM = other.defaultGameplayBGM;

        EnsureSources();
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

        if (subMovementSource == null)
        {
            subMovementSource = gameObject.AddComponent<AudioSource>();
            subMovementSource.loop = true;
            subMovementSource.playOnAwake = false;
            subMovementSource.spatialBlend = 0f;
            subMovementSource.volume = 0f;
        }
        else
        {
            subMovementSource.loop = true;
            subMovementSource.spatialBlend = 0f;
        }

        if (transitionSource == null)
        {
            transitionSource = gameObject.AddComponent<AudioSource>();
            transitionSource.loop = false;
            transitionSource.playOnAwake = false;
            transitionSource.spatialBlend = 0f;
        }
        else
        {
            transitionSource.loop = false;
            transitionSource.spatialBlend = 0f;
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

            if (subMovementSource.outputAudioMixerGroup == null)
            {
                var groups = audioMixer.FindMatchingGroups("SFX");
                if (groups != null && groups.Length > 0)
                    subMovementSource.outputAudioMixerGroup = groups[0];
            }

            if (transitionSource.outputAudioMixerGroup == null)
            {
                var groups = audioMixer.FindMatchingGroups("SFX");
                if (groups != null && groups.Length > 0)
                    transitionSource.outputAudioMixerGroup = groups[0];
            }
        }
    }

    // ── Volume Control (Slider-ready: 0.0001 - 1.0) ───────────────
    public bool  IsMuted      { get; private set; }
    public float MasterVolume { get; private set; } = 1.0f;
    public float BGMVolume    { get; private set; } = 1.0f;
    public float SFXVolume    { get; private set; } = 0.3f;

    public void LoadAudioPreferences()
    {
        MasterVolume = PlayerPrefs.GetFloat("Settings_MasterVol", 1.0f);
        BGMVolume    = PlayerPrefs.GetFloat("Settings_MusicVol", 1.0f);
        SFXVolume    = PlayerPrefs.GetFloat("Settings_SFXVol", SFXVolume);
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
        if (subMovementSource != null && audioMixer == null) subMovementSource.volume = SFXVolume;
        if (transitionSource != null && audioMixer == null) transitionSource.volume = SFXVolume;
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

    public void PlayButtonClick()   => PlaySFX(buttonClickSFX);
    public void PlayCameraShutter() => PlaySFX(cameraShutterSFX);
    public void PlayDebrisFound()   => PlaySFX(debrisFoundSFX);
    public void PlaySpeciesFound()  => PlaySFX(speciesFoundSFX);
    public void PlayDive()          => PlaySFX(diveSFX);
    public void PlaySurface()       => PlaySFX(surfaceSFX);
    public void PlayAlert()         => PlaySFX(alertSFX);

    /// <summary>
    /// Plays the zone transition sound effect for the configured duration (default: 3.0s),
    /// smoothly fading out towards the end.
    /// </summary>
    public void PlayZoneTransition() => PlayZoneTransition(zoneTransitionDuration, zoneTransitionFadeOut);

    /// <summary>
    /// Plays the zone transition sound effect for a specified duration in seconds with a smooth fade-out.
    /// </summary>
    public void PlayZoneTransition(float duration, float fadeOutDuration = 0.5f)
    {
        if (zoneTransitionSFX == null) return;
        EnsureSources();

        if (_transitionCoroutine != null)
        {
            StopCoroutine(_transitionCoroutine);
            _transitionCoroutine = null;
        }

        _transitionCoroutine = StartCoroutine(PlayTimedTransitionRoutine(zoneTransitionSFX, duration, fadeOutDuration));
    }

    private IEnumerator PlayTimedTransitionRoutine(AudioClip clip, float duration, float fadeOutDuration)
    {
        transitionSource.Stop();
        transitionSource.clip = clip;
        float baseVol = (audioMixer != null ? 1f : SFXVolume);
        transitionSource.volume = baseVol;
        transitionSource.Play();

        // If duration is 0 or negative, allow full clip playback
        if (duration <= 0f)
        {
            _transitionCoroutine = null;
            yield break;
        }

        float fadeStart = Mathf.Max(0f, duration - fadeOutDuration);
        float elapsed = 0f;

        // Play at full volume until fade-out window begins
        while (elapsed < fadeStart)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        // Smoothly fade out volume over fadeOutDuration
        float fadeElapsed = 0f;
        float actualFadeDuration = Mathf.Max(0.01f, duration - fadeStart);
        while (fadeElapsed < actualFadeDuration)
        {
            fadeElapsed += Time.unscaledDeltaTime;
            transitionSource.volume = Mathf.Lerp(baseVol, 0f, fadeElapsed / actualFadeDuration);
            yield return null;
        }

        transitionSource.volume = 0f;
        transitionSource.Stop();
        transitionSource.volume = baseVol;
        _transitionCoroutine = null;
    }

    // ── Submarine Movement Loop ───────────────────────────────────
    private float _targetSubMovementVol = 0f;
    private float _currentSubMovementVol = 0f;

    private void Update()
    {
        UpdateSubmarineMovementAudio();
    }

    private void UpdateSubmarineMovementAudio()
    {
        if (subMovementSource == null || subMovementSFX == null) return;

        // Smoothly interpolate current volume towards target volume
        float fadeSpeed = (_targetSubMovementVol > _currentSubMovementVol) ? 4f : 2.5f;
        _currentSubMovementVol = Mathf.MoveTowards(_currentSubMovementVol, _targetSubMovementVol, Time.deltaTime * fadeSpeed);

        float effectiveVol = _currentSubMovementVol * (audioMixer != null ? 1f : SFXVolume);
        subMovementSource.volume = effectiveVol;

        if (_currentSubMovementVol <= 0.001f && _targetSubMovementVol == 0f)
        {
            if (subMovementSource.isPlaying)
            {
                subMovementSource.Stop();
            }
        }
        else if (_currentSubMovementVol > 0.001f)
        {
            if (!subMovementSource.isPlaying)
            {
                subMovementSource.Play();
            }
        }
    }

    /// <summary>
    /// Starts or stops the submarine movement loop sound with smooth volume fading.
    /// Can be called continuously every frame or on input state changes.
    /// </summary>
    public void SetSubmarineMoving(bool isMoving, float intensity = 1f)
    {
        if (subMovementSFX == null) return;
        EnsureSources();

        if (subMovementSource.clip != subMovementSFX)
        {
            subMovementSource.clip = subMovementSFX;
        }

        if (isMoving && intensity > 0.01f)
        {
            _targetSubMovementVol = Mathf.Clamp01(intensity);
            if (!subMovementSource.isPlaying)
            {
                subMovementSource.Play();
            }
        }
        else
        {
            _targetSubMovementVol = 0f;
        }
    }
}