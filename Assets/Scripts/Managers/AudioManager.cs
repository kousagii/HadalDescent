using System;
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
    [Range(0.0f, 2.0f)]
    [Tooltip("Fine-tuning volume multiplier for UI button clicks")]
    [SerializeField] private float buttonClickVolume = 0.80f;

    [Tooltip("Played when clicking the Scan/Camera button")]
    [SerializeField] private AudioClip cameraShutterSFX;
    [Range(0.0f, 2.0f)]
    [Tooltip("Fine-tuning volume multiplier for camera shutter snapshot")]
    [SerializeField] private float cameraShutterVolume = 0.85f;

    [Header("SFX Clips - Detection & Scanning")]
    [Tooltip("Played when the reticle targets and highlights a debris cluster")]
    [SerializeField] private AudioClip debrisFoundSFX;
    [Range(0.0f, 2.0f)]
    [Tooltip("Fine-tuning volume multiplier for debris highlight chime")]
    [SerializeField] private float debrisFoundVolume = 0.85f;

    [Tooltip("Played when the reticle targets and highlights a species")]
    [SerializeField] private AudioClip speciesFoundSFX;
    [Range(0.0f, 2.0f)]
    [Tooltip("Fine-tuning volume multiplier for species lock-on chime")]
    [SerializeField] private float speciesFoundVolume = 0.85f;

    [Header("SFX Clips - Submarine & Movement")]
    [Tooltip("Looping engine / propeller / thruster sound when submarine is moving")]
    [SerializeField] private AudioClip subMovementSFX;
    [SerializeField] private AudioSource subMovementSource;
    [Tooltip("Dedicated AudioMixerGroup for submarine movement. If null, auto-checks for a group named 'Submarine' or falls back to 'SFX'.")]
    [SerializeField] private AudioMixerGroup subMovementMixerGroup;
    [Range(0.1f, 3.0f)]
    [Tooltip("Volume multiplier for submarine movement (1.0 = normal, 1.5 = louder).")]
    [SerializeField] private float subMovementVolumeMultiplier = 2.0f;

    [Header("SFX Clips - Transitions & World")]
    [Tooltip("Transition sound from Zone Selection into gameplay")]
    [SerializeField] private AudioClip zoneTransitionSFX;
    [Tooltip("How long in seconds the zone transition sound will play before stopping (default: 3.0s)")]
    [SerializeField] private float zoneTransitionDuration = 3.0f;
    [Tooltip("Duration in seconds for smooth fade-out at the end of the transition sound (default: 0.5s)")]
    [SerializeField] private float zoneTransitionFadeOut = 0.5f;
    [Range(0.0f, 2.0f)]
    [Tooltip("Fine-tuning volume multiplier for zone dive transition")]
    [SerializeField] private float zoneTransitionVolume = 0.75f;
    [SerializeField] private AudioSource transitionSource;
    [SerializeField] private AudioClip diveSFX;
    [SerializeField] private AudioClip surfaceSFX;
    [SerializeField] private AudioClip alertSFX;

    [Header("Minigame Audio - Clips & SFX")]
    [Tooltip("Looping sound while holding down and bar is rising in Minigame 1 (Capture & Focus)")]
    [SerializeField] private AudioClip minigame1BarRisingSFX;
    [Range(0.0f, 2.0f)]
    [Tooltip("Volume multiplier for minigame 1 bar rising sound (default 0.55 prevents piercing loudness)")]
    [SerializeField] private float minigame1BarVolume = 0.55f;
    [SerializeField] private AudioSource barRisingSource;

    [Tooltip("Sound played when moving / sliding blocks in Minigame 2 (Reconstruction Scan)")]
    [SerializeField] private AudioClip minigame2BlockMoveSFX;
    [Range(0.0f, 2.0f)]
    [Tooltip("Volume multiplier for minigame 2 block sliding")]
    [SerializeField] private float minigame2BlockMoveVolume = 0.90f;

    [Tooltip("Mechanical winch sound while claw drops and rises in Minigame 3 (Environmental Cleanup)")]
    [SerializeField] private AudioClip minigame3ClawSFX;
    [Range(0.0f, 2.0f)]
    [Tooltip("Volume multiplier for minigame 3 claw movement sound (default 0.65)")]
    [SerializeField] private float minigame3ClawVolume = 0.65f;
    [SerializeField] private AudioSource clawSource;

    [Tooltip("Energetic & tropical ambient BGM for Minigame 3")]
    [SerializeField] private AudioClip minigame3BGM;
    [Range(0.0f, 2.0f)]
    [Tooltip("Volume multiplier for Minigame 3 background music")]
    [SerializeField] private float minigame3BGMVolume = 0.70f;

    [Tooltip("Fast-paced panic BGM for Minigame 4 (Hazard Dodge)")]
    [SerializeField] private AudioClip minigame4BGM;
    [Range(0.0f, 2.0f)]
    [Tooltip("Volume multiplier for Minigame 4 panic background music")]
    [SerializeField] private float minigame4BGMVolume = 0.70f;

    [Tooltip("Hazard alert alarm / siren played when entering Minigame 4")]
    [SerializeField] private AudioClip minigame4AlertSFX;
    [Range(0.0f, 2.0f)]
    [Tooltip("Volume multiplier for hazard siren (default 0.45 prevents harsh ear fatigue)")]
    [SerializeField] private float minigame4AlertVolume = 0.45f;

    [Tooltip("Submarine swish / swipe sound when shifting lanes in Minigame 4")]
    [SerializeField] private AudioClip minigame4SwipeSubSFX;
    [Range(0.0f, 2.0f)]
    [Tooltip("Volume multiplier for submarine lane swipe swish (boosted to 1.35 for punchy feedback)")]
    [SerializeField] private float minigame4SwipeSubVolume = 1.35f;

    [Tooltip("Collectible sound when picking up rare pearls / data pods in Minigame 4")]
    [SerializeField] private AudioClip minigame4CollectPearlSFX;
    [Range(0.0f, 2.0f)]
    [Tooltip("Volume multiplier for pearl collect chime")]
    [SerializeField] private float minigame4CollectPearlVolume = 0.85f;

    [Tooltip("Collision bump sound (used for both Minigame 3 species bump and Minigame 4 obstacle bump)")]
    [SerializeField] private AudioClip bumpSFX;
    [Range(0.0f, 2.0f)]
    [Tooltip("Volume multiplier for obstacle and species collision bump")]
    [SerializeField] private float bumpVolume = 0.85f;

    [Header("Gameplay & UI SFX")]
    [Tooltip("Sound played upon purchasing an upgrade in the Upgrade Shop")]
    [SerializeField] private AudioClip buyUpgradeSFX;
    [Range(0.0f, 2.0f)]
    [Tooltip("Volume multiplier for upgrade purchase chime")]
    [SerializeField] private float buyUpgradeVolume = 0.85f;

    [Tooltip("Sound played when a species is successfully scanned")]
    [SerializeField] private AudioClip speciesScannedSuccessSFX;
    [Range(0.0f, 2.0f)]
    [Tooltip("Volume multiplier for species scan success chime")]
    [SerializeField] private float speciesScannedSuccessVolume = 0.90f;

    [Tooltip("Sound played when opening a 3D modal or detail modal in the Bestiary")]
    [SerializeField] private AudioClip openBestiaryModalSFX;
    [Range(0.0f, 2.0f)]
    [Tooltip("Volume multiplier for modal inspection popup")]
    [SerializeField] private float openBestiaryModalVolume = 0.85f;

    [Header("Tutorial & Comms SFX")]
    [SerializeField] private AudioClip radioChirpSFX;
    [Range(0.0f, 2.0f)]
    [Tooltip("Volume multiplier for comms radio chirp")]
    [SerializeField] private float radioChirpVolume = 0.80f;

    [SerializeField] private AudioClip radioStaticSFX;
    [Range(0.0f, 2.0f)]
    [Tooltip("Volume multiplier for comms radio static")]
    [SerializeField] private float radioStaticVolume = 0.65f;

    [SerializeField] private AudioClip tutorialStepChimeSFX;
    [Range(0.0f, 2.0f)]
    [Tooltip("Volume multiplier for tutorial progression chime")]
    [SerializeField] private float tutorialStepChimeVolume = 0.85f;

    [Header("Loudness Normalization")]
    [Tooltip("If enabled, automatically analyzes audio waveforms and normalizes perceived loudness so quiet clips are boosted and loud clips are attenuated to a consistent baseline.")]
    [SerializeField] private bool autoNormalizeSFX = true;

    [Range(0.2f, 1.0f)]
    [Tooltip("Target perceived loudness headroom baseline (0.70 = ~ -3 dBFS).")]
    [SerializeField] private float targetPerceivedLoudness = 0.70f;

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
        EnsureAudioClips();

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
        if (subMovementMixerGroup == null) subMovementMixerGroup = other.subMovementMixerGroup;
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

        if (minigame1BarRisingSFX == null) minigame1BarRisingSFX = other.minigame1BarRisingSFX;
        if (minigame2BlockMoveSFX == null) minigame2BlockMoveSFX = other.minigame2BlockMoveSFX;
        if (minigame3ClawSFX == null)      minigame3ClawSFX      = other.minigame3ClawSFX;
        if (minigame3BGM == null)          minigame3BGM          = other.minigame3BGM;
        if (minigame4BGM == null)          minigame4BGM          = other.minigame4BGM;
        if (minigame4AlertSFX == null)     minigame4AlertSFX     = other.minigame4AlertSFX;
        if (minigame4SwipeSubSFX == null)  minigame4SwipeSubSFX  = other.minigame4SwipeSubSFX;
        if (minigame4CollectPearlSFX == null) minigame4CollectPearlSFX = other.minigame4CollectPearlSFX;
        if (bumpSFX == null)               bumpSFX               = other.bumpSFX;
        if (buyUpgradeSFX == null)         buyUpgradeSFX         = other.buyUpgradeSFX;
        if (speciesScannedSuccessSFX == null) speciesScannedSuccessSFX = other.speciesScannedSuccessSFX;
        if (openBestiaryModalSFX == null)  openBestiaryModalSFX  = other.openBestiaryModalSFX;

        if (radioChirpSFX == null)      radioChirpSFX      = other.radioChirpSFX;
        if (radioStaticSFX == null)     radioStaticSFX     = other.radioStaticSFX;
        if (tutorialStepChimeSFX == null) tutorialStepChimeSFX = other.tutorialStepChimeSFX;

        EnsureAudioClips();
        EnsureSources();
    }

    private void EnsureAudioClips()
    {
        if (radioChirpSFX == null) radioChirpSFX = FindClipByName("radio chirp");
        if (radioStaticSFX == null) radioStaticSFX = FindClipByName("radio static");
        if (tutorialStepChimeSFX == null) tutorialStepChimeSFX = FindClipByName("tutorial step chime");

        if (minigame1BarRisingSFX == null) minigame1BarRisingSFX = FindClipByName("bar filling up");
        if (minigame2BlockMoveSFX == null) minigame2BlockMoveSFX = FindClipByName("button toggle");
        if (minigame3ClawSFX == null) minigame3ClawSFX = FindClipByName("claw dropping");
        if (minigame3BGM == null) minigame3BGM = FindClipByName("minigame 3 bgm");
        if (minigame4BGM == null) minigame4BGM = FindClipByName("minigame 4 bgm");
        if (minigame4AlertSFX == null) minigame4AlertSFX = FindClipByName("hazard alert");
        if (minigame4SwipeSubSFX == null) minigame4SwipeSubSFX = FindClipByName("swipe submarine");
        if (minigame4CollectPearlSFX == null) minigame4CollectPearlSFX = FindClipByName("collect pearl");
        if (bumpSFX == null) bumpSFX = FindClipByName("bump");
        if (buyUpgradeSFX == null) buyUpgradeSFX = FindClipByName("buying an upgrade");
        if (speciesScannedSuccessSFX == null) speciesScannedSuccessSFX = FindClipByName("species scanned successfully");
        if (openBestiaryModalSFX == null) openBestiaryModalSFX = FindClipByName("open bestiary modal");
    }

    private void EnsureTutorialClips() => EnsureAudioClips();

    private void OnDestroy()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        LoadAudioPreferences();
        EnsureTutorialClips();

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
        StopBarRisingSFX();
        StopClawSFX();
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

        if (barRisingSource == null)
        {
            barRisingSource = gameObject.AddComponent<AudioSource>();
            barRisingSource.loop = true;
            barRisingSource.playOnAwake = false;
            barRisingSource.spatialBlend = 0f;
            barRisingSource.volume = 0f;
        }
        else
        {
            barRisingSource.loop = true;
            barRisingSource.spatialBlend = 0f;
        }

        if (clawSource == null)
        {
            clawSource = gameObject.AddComponent<AudioSource>();
            clawSource.loop = true;
            clawSource.playOnAwake = false;
            clawSource.spatialBlend = 0f;
            clawSource.volume = 0f;
        }
        else
        {
            clawSource.loop = true;
            clawSource.spatialBlend = 0f;
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
                if (subMovementMixerGroup != null)
                {
                    subMovementSource.outputAudioMixerGroup = subMovementMixerGroup;
                }
                else
                {
                    var subGroups = audioMixer.FindMatchingGroups("Submarine");
                    if (subGroups != null && subGroups.Length > 0)
                        subMovementSource.outputAudioMixerGroup = subGroups[0];
                    else
                    {
                        var groups = audioMixer.FindMatchingGroups("SFX");
                        if (groups != null && groups.Length > 0)
                            subMovementSource.outputAudioMixerGroup = groups[0];
                    }
                }
            }

            if (transitionSource.outputAudioMixerGroup == null)
            {
                var groups = audioMixer.FindMatchingGroups("SFX");
                if (groups != null && groups.Length > 0)
                    transitionSource.outputAudioMixerGroup = groups[0];
            }

            if (barRisingSource.outputAudioMixerGroup == null)
            {
                var groups = audioMixer.FindMatchingGroups("SFX");
                if (groups != null && groups.Length > 0)
                    barRisingSource.outputAudioMixerGroup = groups[0];
            }

            if (clawSource.outputAudioMixerGroup == null)
            {
                var groups = audioMixer.FindMatchingGroups("SFX");
                if (groups != null && groups.Length > 0)
                    clawSource.outputAudioMixerGroup = groups[0];
            }
        }
    }

    // ── Volume Control (Slider-ready: 0.0001 - 1.0) ───────────────
    public const float SFXBaseVolumeFactor = 0.5f;

    public bool  IsMuted      { get; private set; }
    public float MasterVolume { get; private set; } = 1.0f;
    public float BGMVolume    { get; private set; } = 1.0f;
    public float SFXVolume    { get; private set; } = 1.0f;

    public void LoadAudioPreferences()
    {
        MasterVolume = PlayerPrefs.GetFloat("Settings_MasterVol", 1.0f);
        BGMVolume    = PlayerPrefs.GetFloat("Settings_MusicVol", 1.0f);
        float savedSfx = PlayerPrefs.GetFloat("Settings_SFXVol", 1.0f);
        if (Mathf.Approximately(savedSfx, 0.3f))
        {
            savedSfx = 1.0f;
            PlayerPrefs.SetFloat("Settings_SFXVol", 1.0f);
            PlayerPrefs.Save();
        }
        SFXVolume = savedSfx;
        IsMuted   = PlayerPrefs.GetInt("Settings_Muted", 0) == 1;

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
        float effectiveSFX = SFXVolume * SFXBaseVolumeFactor;
        SetMixerVolume(SFXVolumeParam, effectiveSFX);
        if (sfxSource != null && audioMixer == null) sfxSource.volume = effectiveSFX;
        if (subMovementSource != null && audioMixer == null) subMovementSource.volume = effectiveSFX;
        if (transitionSource != null && audioMixer == null) transitionSource.volume = effectiveSFX;
        if (barRisingSource != null && audioMixer == null) barRisingSource.volume = effectiveSFX * minigame1BarVolume;
        if (clawSource != null && audioMixer == null) clawSource.volume = effectiveSFX * minigame3ClawVolume;
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
    /// Supports custom volume multiplier for track balancing (e.g. minigames).
    /// </summary>
    public void PlayBGM(AudioClip clip, bool fade = true, float volumeMultiplier = 1.0f)
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

        float targetVol = Mathf.Clamp(volumeMultiplier, 0.0001f, 1f);

        if (fade && fadeDuration > 0f && bgmSource.isPlaying)
        {
            _fadeCoroutine = StartCoroutine(FadeToNewBGM(clip, fadeDuration, targetVol));
        }
        else
        {
            bgmSource.clip = clip;
            bgmSource.volume = targetVol;
            bgmSource.Play();
        }
    }

    private IEnumerator FadeToNewBGM(AudioClip newClip, float duration, float targetVolume = 1.0f)
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
            bgmSource.volume = Mathf.Lerp(0f, targetVolume, t / halfDuration);
            yield return null;
        }
        bgmSource.volume = targetVolume;
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

    // ── SFX Playback & Loudness Normalization ──────────────────────
    private readonly System.Collections.Generic.Dictionary<AudioClip, float> _normalizationCache = new System.Collections.Generic.Dictionary<AudioClip, float>();

    /// <summary>
    /// Computes or retrieves cached loudness normalization gain for the given AudioClip.
    /// Analyzes Peak amplitude and RMS energy to calculate perceived loudness,
    /// returning a scale factor that brings quiet or loud clips into consistent loudness balance.
    /// </summary>
    public float GetClipNormalizationGain(AudioClip clip)
    {
        if (clip == null) return 1.0f;
        if (_normalizationCache.TryGetValue(clip, out float cachedGain))
            return cachedGain;

        float calculatedGain = CalculateClipGain(clip);
        _normalizationCache[clip] = calculatedGain;
        return calculatedGain;
    }

    private float CalculateClipGain(AudioClip clip)
    {
        try
        {
            int totalSamples = clip.samples * clip.channels;
            if (totalSamples <= 0) return 1.0f;

            // Sample up to 1 second of audio (or entire clip if shorter)
            int sampleCount = Mathf.Min(totalSamples, 44100);
            float[] samples = new float[sampleCount];

            if (!clip.GetData(samples, 0))
            {
                return 1.0f;
            }

            float peak = 0f;
            double sumSquares = 0.0;

            for (int i = 0; i < sampleCount; i++)
            {
                float absVal = Mathf.Abs(samples[i]);
                if (absVal > peak) peak = absVal;
                sumSquares += absVal * absVal;
            }

            float rms = Mathf.Sqrt((float)(sumSquares / sampleCount));
            // Perceived loudness blends Peak (transient impact) and RMS (sustained acoustic power)
            float perceivedLoudness = Mathf.Max(0.01f, (peak * 0.6f) + (rms * 2.5f * 0.4f));

            // Gain needed to bring clip to targetPerceivedLoudness headroom
            float gain = targetPerceivedLoudness / perceivedLoudness;

            // Safe clamping: attenuate loud sounds down to 0.35x, boost quiet sounds up to 2.0x
            gain = Mathf.Clamp(gain, 0.35f, 2.0f);
            return gain;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[AudioManager] Loudness normalization analysis skipped for '{clip.name}': {ex.Message}");
            return 1.0f;
        }
    }

    public void PlaySFX(AudioClip clip) => PlaySFX(clip, 1.0f);

    public void PlaySFX(AudioClip clip, float volumeMultiplier)
    {
        if (clip == null) return;
        EnsureSources();

        float normGain = autoNormalizeSFX ? GetClipNormalizationGain(clip) : 1.0f;
        float finalScale = Mathf.Clamp(volumeMultiplier * normGain, 0.05f, 2.0f);
        sfxSource.PlayOneShot(clip, finalScale);
    }

    public void PlayButtonClick()   => PlaySFX(buttonClickSFX, buttonClickVolume);
    public void PlayCameraShutter() => PlaySFX(cameraShutterSFX, cameraShutterVolume);
    public void PlayDebrisFound()   => PlaySFX(debrisFoundSFX, debrisFoundVolume);
    public void PlaySpeciesFound()  => PlaySFX(speciesFoundSFX, speciesFoundVolume);
    public void PlayDive()          => PlaySFX(diveSFX);
    public void PlaySurface()       => PlaySFX(surfaceSFX);
    public void PlayAlert()         => PlaySFX(alertSFX);

    public void PlayRadioChirp()
    {
        if (radioChirpSFX == null) radioChirpSFX = FindClipByName("radio chirp");
        if (radioChirpSFX != null) PlaySFX(radioChirpSFX, radioChirpVolume);
        else Debug.LogWarning("[AudioManager] Radio Chirp SFX not assigned or found.");
    }

    public void PlayRadioStatic()
    {
        if (radioStaticSFX == null) radioStaticSFX = FindClipByName("radio static");
        if (radioStaticSFX != null) PlaySFX(radioStaticSFX, radioStaticVolume);
        else Debug.LogWarning("[AudioManager] Radio Static SFX not assigned or found.");
    }

    public void PlayTutorialStepChime()
    {
        if (tutorialStepChimeSFX == null) tutorialStepChimeSFX = FindClipByName("tutorial step chime");
        if (tutorialStepChimeSFX != null) PlaySFX(tutorialStepChimeSFX, tutorialStepChimeVolume);
        else Debug.LogWarning("[AudioManager] Tutorial Step Chime SFX not assigned or found.");
    }

    // ── Minigame BGM & Loops ─────────────────────────────────────

    /// <summary>
    /// Plays energetic & tropical ambient BGM for Minigame 3 (Environmental Cleanup).
    /// </summary>
    public void PlayMinigame3BGM()
    {
        if (minigame3BGM == null) minigame3BGM = FindClipByName("minigame 3 bgm");
        if (minigame3BGM != null)
        {
            float normGain = autoNormalizeSFX ? GetClipNormalizationGain(minigame3BGM) : 1.0f;
            PlayBGM(minigame3BGM, fade: true, volumeMultiplier: minigame3BGMVolume * normGain);
        }
    }

    /// <summary>
    /// Plays fast-paced panic BGM for Minigame 4 (Hazard Dodge).
    /// </summary>
    public void PlayMinigame4BGM()
    {
        if (minigame4BGM == null) minigame4BGM = FindClipByName("minigame 4 bgm");
        if (minigame4BGM != null)
        {
            float normGain = autoNormalizeSFX ? GetClipNormalizationGain(minigame4BGM) : 1.0f;
            PlayBGM(minigame4BGM, fade: true, volumeMultiplier: minigame4BGMVolume * normGain);
        }
    }

    /// <summary>
    /// Restores the exploration background music for the current ocean zone.
    /// Also halts any minigame loops (bar rising, claw) to ensure zero sound bleed.
    /// </summary>
    public void RestoreZoneBGM()
    {
        StopBarRisingSFX();
        StopClawSFX();
        PlayZoneBGM(ZoneManager.CurrentZoneIndex);
    }

    /// <summary>
    /// Starts or stops the looping focus bar rising sound for Minigame 1.
    /// Hard unconditional stop, zero volume, and clip clearing to guarantee zero sound bleed.
    /// </summary>
    public void SetBarRising(bool isRising)
    {
        EnsureSources();
        if (minigame1BarRisingSFX == null)
            minigame1BarRisingSFX = FindClipByName("bar filling up");

        if (barRisingSource == null || minigame1BarRisingSFX == null) return;

        if (isRising)
        {
            if (barRisingSource.clip != minigame1BarRisingSFX)
            {
                barRisingSource.clip = minigame1BarRisingSFX;
            }
            barRisingSource.loop = true;

            float normGain = autoNormalizeSFX ? GetClipNormalizationGain(minigame1BarRisingSFX) : 1.0f;
            float effectiveVol = Mathf.Clamp(minigame1BarVolume * normGain, 0f, 1.5f) * (audioMixer != null ? 1f : SFXVolume * SFXBaseVolumeFactor);
            barRisingSource.volume = effectiveVol;

            if (!barRisingSource.isPlaying)
            {
                barRisingSource.Play();
            }
        }
        else
        {
            // Unconditional hard stop, zero volume, remove clip, and disable loop to guarantee zero audio leak
            barRisingSource.Stop();
            barRisingSource.volume = 0f;
            barRisingSource.clip = null;
            barRisingSource.loop = false;
        }
    }

    public void PlayBarRisingSFX() => SetBarRising(true);
    public void StopBarRisingSFX() => SetBarRising(false);

    /// <summary>
    /// Starts or stops the looping mechanical winch claw sound for Minigame 3 (dropping and ascending).
    /// Hard unconditional stop, zero volume, and clip clearing to guarantee zero sound bleed.
    /// </summary>
    public void SetClawMoving(bool isMoving)
    {
        EnsureSources();
        if (minigame3ClawSFX == null)
            minigame3ClawSFX = FindClipByName("claw dropping");

        if (clawSource == null || minigame3ClawSFX == null) return;

        if (isMoving)
        {
            if (clawSource.clip != minigame3ClawSFX)
            {
                clawSource.clip = minigame3ClawSFX;
            }
            clawSource.loop = true;

            float normGain = autoNormalizeSFX ? GetClipNormalizationGain(minigame3ClawSFX) : 1.0f;
            float effectiveVol = Mathf.Clamp(minigame3ClawVolume * normGain, 0f, 1.5f) * (audioMixer != null ? 1f : SFXVolume * SFXBaseVolumeFactor);
            clawSource.volume = effectiveVol;

            if (!clawSource.isPlaying)
            {
                clawSource.Play();
            }
        }
        else
        {
            // Unconditional hard stop, zero volume, remove clip, and disable loop
            clawSource.Stop();
            clawSource.volume = 0f;
            clawSource.clip = null;
            clawSource.loop = false;
        }
    }

    public void PlayClawSFX() => SetClawMoving(true);
    public void StopClawSFX() => SetClawMoving(false);

    public void PlayBlockMoveSFX()
    {
        if (minigame2BlockMoveSFX == null) minigame2BlockMoveSFX = FindClipByName("button toggle");
        PlaySFX(minigame2BlockMoveSFX, minigame2BlockMoveVolume);
    }

    public void PlayHazardAlertSFX()
    {
        if (minigame4AlertSFX == null) minigame4AlertSFX = FindClipByName("hazard alert");
        PlaySFX(minigame4AlertSFX != null ? minigame4AlertSFX : alertSFX, minigame4AlertVolume);
    }

    public void PlaySubSwipeSFX()
    {
        if (minigame4SwipeSubSFX == null) minigame4SwipeSubSFX = FindClipByName("swipe submarine");
        PlaySFX(minigame4SwipeSubSFX, minigame4SwipeSubVolume);
    }

    public void PlayPearlCollectSFX()
    {
        if (minigame4CollectPearlSFX == null) minigame4CollectPearlSFX = FindClipByName("collect pearl");
        PlaySFX(minigame4CollectPearlSFX, minigame4CollectPearlVolume);
    }

    public void PlayBumpSFX()
    {
        if (bumpSFX == null) bumpSFX = FindClipByName("bump");
        PlaySFX(bumpSFX, bumpVolume);
    }

    public void PlayBuyUpgradeSFX()
    {
        if (buyUpgradeSFX == null) buyUpgradeSFX = FindClipByName("buying an upgrade");
        PlaySFX(buyUpgradeSFX, buyUpgradeVolume);
    }

    public void PlaySpeciesScannedSuccessSFX()
    {
        // Stop any focus bar rising sound that might be lingering from minigame
        StopBarRisingSFX();
        if (speciesScannedSuccessSFX == null) speciesScannedSuccessSFX = FindClipByName("species scanned successfully");
        PlaySFX(speciesScannedSuccessSFX, speciesScannedSuccessVolume);
    }

    public void PlayOpenBestiaryModalSFX()
    {
        if (openBestiaryModalSFX == null) openBestiaryModalSFX = FindClipByName("open bestiary modal");
        PlaySFX(openBestiaryModalSFX, openBestiaryModalVolume);
    }

    // Public properties for runtime / Inspector queries
    public bool AutoNormalizeSFX          { get => autoNormalizeSFX; set => autoNormalizeSFX = value; }
    public float TargetPerceivedLoudness   { get => targetPerceivedLoudness; set => targetPerceivedLoudness = value; }
    public float ButtonClickVolume        { get => buttonClickVolume; set => buttonClickVolume = value; }
    public float CameraShutterVolume      { get => cameraShutterVolume; set => cameraShutterVolume = value; }
    public float DebrisFoundVolume        { get => debrisFoundVolume; set => debrisFoundVolume = value; }
    public float SpeciesFoundVolume       { get => speciesFoundVolume; set => speciesFoundVolume = value; }
    public float ZoneTransitionVolume     { get => zoneTransitionVolume; set => zoneTransitionVolume = value; }
    public float Minigame1BarVolume       { get => minigame1BarVolume; set => minigame1BarVolume = value; }
    public float Minigame2BlockMoveVolume { get => minigame2BlockMoveVolume; set => minigame2BlockMoveVolume = value; }
    public float Minigame3ClawVolume      { get => minigame3ClawVolume; set => minigame3ClawVolume = value; }
    public float Minigame3BGMVolume       { get => minigame3BGMVolume; set => minigame3BGMVolume = value; }
    public float Minigame4BGMVolume       { get => minigame4BGMVolume; set => minigame4BGMVolume = value; }
    public float Minigame4AlertVolume     { get => minigame4AlertVolume; set => minigame4AlertVolume = value; }
    public float Minigame4SwipeSubVolume  { get => minigame4SwipeSubVolume; set => minigame4SwipeSubVolume = value; }
    public float Minigame4CollectPearlVolume { get => minigame4CollectPearlVolume; set => minigame4CollectPearlVolume = value; }
    public float BumpVolume               { get => bumpVolume; set => bumpVolume = value; }
    public float BuyUpgradeVolume         { get => buyUpgradeVolume; set => buyUpgradeVolume = value; }
    public float SpeciesScannedSuccessVolume { get => speciesScannedSuccessVolume; set => speciesScannedSuccessVolume = value; }
    public float OpenBestiaryModalVolume  { get => openBestiaryModalVolume; set => openBestiaryModalVolume = value; }
    public float RadioChirpVolume         { get => radioChirpVolume; set => radioChirpVolume = value; }
    public float RadioStaticVolume        { get => radioStaticVolume; set => radioStaticVolume = value; }
    public float TutorialStepChimeVolume  { get => tutorialStepChimeVolume; set => tutorialStepChimeVolume = value; }

    public AudioClip Minigame1BarRisingSFX { get => minigame1BarRisingSFX; set => minigame1BarRisingSFX = value; }
    public AudioClip Minigame2BlockMoveSFX { get => minigame2BlockMoveSFX; set => minigame2BlockMoveSFX = value; }
    public AudioClip Minigame3ClawSFX      { get => minigame3ClawSFX; set => minigame3ClawSFX = value; }
    public AudioClip Minigame3BGM          { get => minigame3BGM; set => minigame3BGM = value; }
    public AudioClip Minigame4BGM          { get => minigame4BGM; set => minigame4BGM = value; }
    public AudioClip Minigame4AlertSFX     { get => minigame4AlertSFX; set => minigame4AlertSFX = value; }
    public AudioClip Minigame4SwipeSubSFX  { get => minigame4SwipeSubSFX; set => minigame4SwipeSubSFX = value; }
    public AudioClip Minigame4CollectPearlSFX { get => minigame4CollectPearlSFX; set => minigame4CollectPearlSFX = value; }
    public AudioClip BumpSFX               { get => bumpSFX; set => bumpSFX = value; }
    public AudioClip BuyUpgradeSFX         { get => buyUpgradeSFX; set => buyUpgradeSFX = value; }
    public AudioClip SpeciesScannedSuccessSFX { get => speciesScannedSuccessSFX; set => speciesScannedSuccessSFX = value; }
    public AudioClip OpenBestiaryModalSFX  { get => openBestiaryModalSFX; set => openBestiaryModalSFX = value; }

    private AudioClip FindClipByName(string clipName)
    {
        // 1. Try Resources.Load from "Audio/" folder
        var clip = Resources.Load<AudioClip>("Audio/" + clipName);
        if (clip != null) return clip;

        // 2. Try Resources.Load from root Resources folder
        clip = Resources.Load<AudioClip>(clipName);
        if (clip != null) return clip;

        // 3. Fallback: Search all loaded AudioClips in memory
        var allClips = Resources.FindObjectsOfTypeAll<AudioClip>();
        foreach (var c in allClips)
        {
            if (c != null && c.name.Equals(clipName, StringComparison.OrdinalIgnoreCase))
                return c;
        }
        return null;
    }

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
        float normGain = autoNormalizeSFX ? GetClipNormalizationGain(clip) : 1.0f;
        float baseVol = Mathf.Clamp(zoneTransitionVolume * normGain, 0f, 1.5f) * (audioMixer != null ? 1f : SFXVolume * SFXBaseVolumeFactor);
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

        float effectiveVol = Mathf.Clamp01(_currentSubMovementVol * subMovementVolumeMultiplier * (audioMixer != null ? 1f : SFXVolume * SFXBaseVolumeFactor));
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