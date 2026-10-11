using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Configuration data for each submarine light upgrade tier.
/// </summary>
[System.Serializable]
public struct LightTierSettings
{
    [Tooltip("Tier index (1 to 5)")]
    public int tier;

    [Tooltip("Display name / type of lighting technology")]
    public string tierName;

    [Tooltip("Maximum illumination reach in meters")]
    public float range;

    [Tooltip("Outer spot cone angle in degrees")]
    public float spotAngle;

    [Tooltip("Inner spot cone angle (smooth falloff edge) in degrees")]
    public float innerSpotAngle;

    [Tooltip("Light intensity (in URP Lux/Lumens)")]
    public float intensity;

    [Tooltip("Light color and chromatic temperature")]
    public Color color;

    [Tooltip("Fog penetration bonus added to RenderSettings.fogEndDistance")]
    public float fogEndBonus;

    [Tooltip("Near-field fog clearance added to RenderSettings.fogStartDistance")]
    public float fogStartBonus;
}

/// <summary>
/// Controls the submarine's exterior lighting rig mounted on the player's Main Camera.
/// 
/// Key Features:
/// 1. Synchronizes directly with GameManager.Instance.LightTier (Tier 1 to 5).
/// 2. Implements dynamic Fog Penetration Bonus: higher light tiers push back dense deep-sea fog,
///    allowing illumination reach to work even in Midnight (85m), Abyss (75m), and Hadal (65m) zones.
/// 3. Procedural Auto-Rig: automatically constructs a calibrated dual-boom spotlight rig with
///    ambient cockpit fill if lights are not manually wired in the Inspector.
/// 4. Smooth Fog Transition: smoothly blends fog distance when headlights are toggled ON/OFF.
/// 5. Hotkey toggle ('L' key) and public API for UI HUD buttons.
/// 6. Cockpit-safe beam readability: because the rig is mounted on the camera, the player is always
///    INSIDE the beam, so geometry cones only produce full-screen haze. Instead the beam is conveyed by
///    what it lights up: marine snow (3m+ ahead, camera-faded), a defined-edge light cookie, and a
///    subtle lit-water fog tint.
/// </summary>
public class SubmarineLightController : MonoBehaviour
{
    public static SubmarineLightController Instance { get; private set; }
    public static event System.Action<bool> OnLightsChanged;

    // -----------------------------------------------------------------------
    // Inspector Light References
    // -----------------------------------------------------------------------

    [Header("Hierarchy & Rig")]
    [Tooltip("Root Transform holding all submarine lights. Auto-created if empty.")]
    [SerializeField] private Transform lightRigRoot;

    [Tooltip("Port (Left) forward spotlight.")]
    [SerializeField] private Light portSpotlight;

    [Tooltip("Starboard (Right) forward spotlight.")]
    [SerializeField] private Light starboardSpotlight;

    [Tooltip("Soft ambient cockpit / hull fill light to illuminate immediate foreground.")]
    [SerializeField] private Light cockpitFillLight;

    [Header("Audio Feedback")]
    [Tooltip("Click/hum sound played when headlights are toggled.")]
    [SerializeField] private AudioClip lightToggleSFX;
    private AudioSource _audioSource;

    [Header("Beam Readability (Cockpit-Safe)")]
    [Tooltip("Multiplier on tier intensity for the two boom lamps (compensates for the removed centre lamp).")]
    [Range(0.5f, 2.5f)]
    [SerializeField] private float lampIntensityMultiplier = 1.3f;

    [Tooltip("Projects a procedural cookie (bright core + defined edge ring) so the lit area on seabed, rocks and creatures has a readable spotlight shape. Requires 'Light Cookies' enabled in the URP Asset.")]
    [SerializeField] private bool useLightCookie = true;

    [Tooltip("How pronounced the cookie's hotspot/edge ring is. 0 = flat soft disc, 1 = strong ring.")]
    [Range(0f, 1f)]
    [SerializeField] private float cookieStrength = 0.75f;

    [Tooltip("How much the fog colour is tinted toward the beam colour while lights are ON (lit-water feel). Keep small.")]
    [Range(0f, 0.2f)]
    [SerializeField] private float fogTintAmount = 0.06f;

    [Tooltip("Seconds for snow brightness and fog tint to blend when lights toggle.")]
    [Range(0.05f, 2f)]
    [SerializeField] private float lightBlendDuration = 0.4f;

    [Header("Marine Snow (Suspended Particulate)")]
    [Tooltip("Small drifting specks ahead of the submarine. Faint when lights are OFF, brighter when ON.")]
    [SerializeField] private bool showMarineSnow = true;

    [Tooltip("Maximum number of snow particles alive at once.")]
    [Range(10, 150)]
    [SerializeField] private int marineSnowMaxParticles = 60;

    [Tooltip("Snow brightness while headlights are ON.")]
    [Range(0f, 1f)]
    [SerializeField] private float snowBrightnessOn = 0.45f;

    [Tooltip("Snow brightness while headlights are OFF.")]
    [Range(0f, 1f)]
    [SerializeField] private float snowBrightnessOff = 0.12f;

    [Tooltip("Optional material override for the snow (use if URP Particles/Unlit is stripped from builds).")]
    [SerializeField] private Material marineSnowMaterialOverride;

    [Header("Debug")]
    [Tooltip("Disables marine snow, cookie and fog tint (spotlights and fog penetration still work).")]
    [SerializeField] private bool disableAllBeamEffects = false;

    // Marine snow layout (metres, relative to the rig): particles only spawn between these distances
    private const float SnowNearDistance = 3f;
    private const float SnowFarDistance  = 25f;
    private const float SnowFadeStart    = 2f;   // fully invisible closer than this
    private const float SnowFadeEnd      = 5f;   // fully visible beyond this

    // -----------------------------------------------------------------------
    // Default Upgrade Tier Configurations
    // -----------------------------------------------------------------------

    [Header("Tier Presets (1 to 5)")]
    [SerializeField]
    private LightTierSettings[] tierSettings = new LightTierSettings[]
    {
        // Tier 1: 25m reach | Narrow halogen beam (Sunlight Zone) | Default (0 RDP)
        new LightTierSettings
        {
            tier               = 1,
            tierName           = "Narrow Halogen Beam",
            range              = 25f,
            spotAngle          = 32f,
            innerSpotAngle     = 20f,
            intensity          = 2.8f,
            color              = new Color(1.00f, 0.90f, 0.72f, 1f), // Warm Halogen (~3200K)
            fogEndBonus        = 14f,
            fogStartBonus      = 3f
        },
        // Tier 2: 50m reach | High-intensity LED beam (45° angle) | 150 RDP
        new LightTierSettings
        {
            tier               = 2,
            tierName           = "High-Intensity LED",
            range              = 50f,
            spotAngle          = 45f,
            innerSpotAngle     = 32f,
            intensity          = 4.0f,
            color              = new Color(0.92f, 0.97f, 1.00f, 1f), // Crisp Cool White (~5500K)
            fogEndBonus        = 15f,
            fogStartBonus      = 4f
        },
        // Tier 3: 80m reach | Wide floodlight matrix (75° angle) | 350 RDP
        new LightTierSettings
        {
            tier               = 3,
            tierName           = "Wide Floodlight Matrix",
            range              = 80f,
            spotAngle          = 75f,
            innerSpotAngle     = 55f,
            intensity          = 5.5f,
            color              = new Color(0.96f, 0.98f, 1.00f, 1f), // Neutral Daylight (~6200K)
            fogEndBonus        = 35f,
            fogStartBonus      = 8f
        },
        // Tier 4: 120m reach | Deep-penetrating xenon spotlights (100° angle) | 750 RDP
        new LightTierSettings
        {
            tier               = 4,
            tierName           = "Deep-Penetrating Xenon",
            range              = 120f,
            spotAngle          = 100f,
            innerSpotAngle     = 75f,
            intensity          = 7.5f,
            color              = new Color(0.82f, 0.91f, 1.00f, 1f), // Deep Xenon Piercing Ice Blue (~7200K)
            fogEndBonus        = 65f,
            fogStartBonus      = 14f
        },
        // Tier 5: 170m reach | 120° angle arc floodlight | 1,500 RDP
        new LightTierSettings
        {
            tier               = 5,
            tierName           = "Full-Field Arc Matrix",
            range              = 170f,
            spotAngle          = 120f,
            innerSpotAngle     = 90f,
            intensity          = 9.5f,
            color              = new Color(0.90f, 0.95f, 1.00f, 1f), // Ultra-Lumen Arc Luminescence (~6500K)
            fogEndBonus        = 105f,
            fogStartBonus      = 20f
        }
    };

    // -----------------------------------------------------------------------
    // Runtime State
    // -----------------------------------------------------------------------

    private bool  _isLightOn           = true;
    private int   _currentAppliedTier = -1;
    private Camera _cam;
    private Coroutine _fogBlendCoroutine;

    // 0 = lights fully off, 1 = lights fully on (smoothed)
    private float _lightBlend = 1f;
    private bool  _refreshRequested;

    // Marine snow
    private ParticleSystem _snowSystem;
    private Material _snowMaterial;
    private static Texture2D _snowDotTexture;

    // Cookie
    private static Texture2D _beamCookie;
    private static float _beamCookieStrength = -1f;

    // Fog tint bookkeeping (ZoneManager owns the base fog colour; we only add a small tint on top)
    private Color _baseFogColor;
    private Color _lastWrittenFogColor;
    private bool  _hasFogBase;

    public bool AreLightsOn => _isLightOn;
    public int CurrentTierNumber => _currentAppliedTier;

    public float CurrentFogEndBonus
    {
        get
        {
            if (!_isLightOn) return 0f;
            int idx = Mathf.Clamp((_currentAppliedTier > 0 ? _currentAppliedTier : 1) - 1, 0, tierSettings.Length - 1);
            return tierSettings[idx].fogEndBonus;
        }
    }

    public float CurrentFogStartBonus
    {
        get
        {
            if (!_isLightOn) return 0f;
            int idx = Mathf.Clamp((_currentAppliedTier > 0 ? _currentAppliedTier : 1) - 1, 0, tierSettings.Length - 1);
            return tierSettings[idx].fogStartBonus;
        }
    }

    private LightTierSettings CurrentTier
    {
        get
        {
            int idx = Mathf.Clamp((_currentAppliedTier > 0 ? _currentAppliedTier : 1) - 1, 0, tierSettings.Length - 1);
            return tierSettings[idx];
        }
    }

    // -----------------------------------------------------------------------
    // Unity Lifecycle
    // -----------------------------------------------------------------------

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(this);
            return;
        }

        _cam = GetComponent<Camera>();
        if (_cam == null) _cam = Camera.main;
        _audioSource = GetComponent<AudioSource>();
        if (_audioSource == null)
        {
            _audioSource = gameObject.AddComponent<AudioSource>();
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0f; // 2D interior cockpit click
        }

        EnsureLightRigBuilt();
    }

    private void Start()
    {
        // Subscribe to Shop upgrades
        if (ShopManager.Instance != null)
        {
            ShopManager.Instance.OnUpgradePurchased += OnUpgradePurchased;
        }

        _lightBlend = _isLightOn ? 1f : 0f;

        // Apply initial tier from save
        int initialTier = GameManager.Instance != null ? GameManager.Instance.LightTier : 1;
        ApplyTier(initialTier, instantFog: true);
    }

    private void OnDestroy()
    {
        if (ShopManager.Instance != null)
        {
            ShopManager.Instance.OnUpgradePurchased -= OnUpgradePurchased;
        }

        // Hand the untinted fog colour back to the scene
        if (_hasFogBase && Application.isPlaying)
        {
            RenderSettings.fogColor = _baseFogColor;
        }

        if (_snowMaterial != null)
        {
            if (Application.isPlaying) Destroy(_snowMaterial);
            else DestroyImmediate(_snowMaterial);
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnValidate()
    {
        // Re-apply Inspector tweaks live during Play Mode
        if (Application.isPlaying && _currentAppliedTier > 0)
        {
            _refreshRequested = true;
        }
    }

    private void Update()
    {
        // 1. Hotkey toggle ('L' on keyboard)
        CheckInputToggle();

        // 2. Poll for tier change if GameManager changed outside ShopManager
        int activeTier = GameManager.Instance != null ? GameManager.Instance.LightTier : 1;
        if (activeTier != _currentAppliedTier && activeTier > 0)
        {
            ApplyTier(activeTier, instantFog: false);
        }
        else if (_refreshRequested)
        {
            _refreshRequested = false;
            ApplyTier(_currentAppliedTier, instantFog: true);
        }

        // 3. Smoothly blend snow brightness + fog tint toward the light state
        float target = _isLightOn ? 1f : 0f;
        _lightBlend = Mathf.MoveTowards(_lightBlend, target, Time.deltaTime / Mathf.Max(0.01f, lightBlendDuration));

        UpdateMarineSnowAppearance();
        UpdateFogTint();
    }

    private void CheckInputToggle()
    {
        // Pure New Input System check — avoids InvalidOperationException in projects using New Input System package
        bool toggleRequested = false;

        if (Keyboard.current != null && Keyboard.current.lKey.wasPressedThisFrame)
        {
            toggleRequested = true;
        }
        else if (Gamepad.current != null && Gamepad.current.dpad.up.wasPressedThisFrame)
        {
            toggleRequested = true;
        }

        if (toggleRequested)
        {
            ToggleLights();
        }
    }

    private void OnUpgradePurchased(UpgradeCategory category, int newTier)
    {
        if (category == UpgradeCategory.Lights)
        {
            Debug.Log($"[SubmarineLightController] Upgraded Submarine Lights to Tier {newTier}!");
            ApplyTier(newTier, instantFog: false);
        }
    }

    // -----------------------------------------------------------------------
    // Core Lighting Logic
    // -----------------------------------------------------------------------

    /// <summary>
    /// Applies parameters for the specified tier (1 through 5).
    /// </summary>
    public void ApplyTier(int tierNumber, bool instantFog = false)
    {
        int clampedTier = Mathf.Clamp(tierNumber, 1, 5);
        _currentAppliedTier = clampedTier;
        int index = clampedTier - 1;

        LightTierSettings settings = tierSettings[index];

        // Apply to physical lights
        ApplySettingsToLight(portSpotlight, settings);
        ApplySettingsToLight(starboardSpotlight, settings);

        // Adjust cockpit ambient fill brightness proportionally
        if (cockpitFillLight != null)
        {
            cockpitFillLight.range = 5f + clampedTier;
            cockpitFillLight.intensity = 0.25f + clampedTier * 0.10f;
            cockpitFillLight.color = Color.Lerp(settings.color, Color.white, 0.4f);
        }

        // Marine snow on/off according to settings
        EnsureMarineSnowVFX();

        // Apply Fog Penetration Bonus
        ApplyFogPenetration(settings, instantFog);

        Debug.Log($"[SubmarineLightController] Applied Tier {clampedTier} ({settings.tierName}) | Reach: {settings.range}m | Angle: {settings.spotAngle}° | Fog Bonus: +{settings.fogEndBonus}m");
    }

    private void ApplySettingsToLight(Light l, LightTierSettings s)
    {
        if (l == null) return;
        l.range          = s.range;
        l.spotAngle      = s.spotAngle;
        l.innerSpotAngle = s.innerSpotAngle;
        l.intensity      = s.intensity * lampIntensityMultiplier;
        l.color          = s.color;
        l.enabled        = _isLightOn;

        // Defined spotlight shape on whatever the beam hits
        l.cookie = (useLightCookie && !disableAllBeamEffects) ? GetBeamCookie(cookieStrength) : null;
    }

    /// <summary>
    /// Toggles headlights on or off.
    /// </summary>
    public void ToggleLights()
    {
        SetLightsState(!_isLightOn);
    }

    public void SetLightsState(bool turnOn)
    {
        _isLightOn = turnOn;
        OnLightsChanged?.Invoke(_isLightOn);

        if (portSpotlight != null)      portSpotlight.enabled = _isLightOn;
        if (starboardSpotlight != null) starboardSpotlight.enabled = _isLightOn;
        if (cockpitFillLight != null)   cockpitFillLight.enabled = _isLightOn;

        // Snow keeps drifting either way; its brightness blends in Update()

        if (lightToggleSFX != null && _audioSource != null)
        {
            _audioSource.pitch = _isLightOn ? 1.05f : 0.90f;
            _audioSource.PlayOneShot(lightToggleSFX);
        }

        // Update fog penetration
        ApplyFogPenetration(CurrentTier, instantFog: false);

        Debug.Log($"[SubmarineLightController] Headlights turned {(_isLightOn ? "ON" : "OFF")}");
    }

    // -----------------------------------------------------------------------
    // Light Cookie (defined spotlight edge on lit surfaces)
    // -----------------------------------------------------------------------

    private static Texture2D GetBeamCookie(float strength)
    {
        if (_beamCookie != null && Mathf.Abs(_beamCookieStrength - strength) < 0.001f)
            return _beamCookie;

        if (_beamCookie != null)
        {
            if (Application.isPlaying) Destroy(_beamCookie);
            else DestroyImmediate(_beamCookie);
        }

        const int size = 128;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
        {
            name = "Tex_SubmarineBeamCookie",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        var pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = ((x + 0.5f) / size) * 2f - 1f;
                float v = ((y + 0.5f) / size) * 2f - 1f;
                float r = Mathf.Sqrt(u * u + v * v); // 0 centre, 1 edge

                // Soft fade to black well before the texture border (required for spot cookies)
                float edgeMask = 1f - Mathf.SmoothStep(0.80f, 0.97f, r);

                // Bright hotspot that eases down toward the rim
                float core = 1f - Mathf.SmoothStep(0f, 0.75f, r) * 0.45f;

                // Slightly brighter ring that gives the lit area a readable edge
                float ringT = (r - 0.68f) / 0.07f;
                float ring = Mathf.Exp(-ringT * ringT) * 0.35f;

                float shaped = Mathf.Clamp01(core + ring) * edgeMask;
                float val = Mathf.Lerp(edgeMask, shaped, strength);

                if (x == 0 || y == 0 || x == size - 1 || y == size - 1) val = 0f;

                pixels[y * size + x] = new Color(val, val, val, val);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply(false, true);

        _beamCookie = tex;
        _beamCookieStrength = strength;
        return _beamCookie;
    }

    // -----------------------------------------------------------------------
    // Marine Snow (cockpit-safe: spawns 3–25m ahead, fades out near the lens)
    // -----------------------------------------------------------------------

    private void EnsureMarineSnowVFX()
    {
        if (lightRigRoot == null) return;

        Transform snowTr = lightRigRoot.Find("MarineSnow_VFX");
        GameObject snowGO = snowTr != null ? snowTr.gameObject : null;
        bool wantSnow = showMarineSnow && !disableAllBeamEffects;

        if (snowGO == null)
        {
            if (!wantSnow) return;
            snowGO = new GameObject("MarineSnow_VFX");
            snowGO.transform.SetParent(lightRigRoot, false);
        }

        // Emitter sits at the rig origin; the spawn box itself is offset forward
        snowGO.transform.localPosition = Vector3.zero;
        snowGO.transform.localRotation = Quaternion.identity;

        _snowSystem = snowGO.GetComponent<ParticleSystem>();
        if (_snowSystem == null) _snowSystem = snowGO.AddComponent<ParticleSystem>();
        var psr = snowGO.GetComponent<ParticleSystemRenderer>();
        if (psr == null) psr = snowGO.AddComponent<ParticleSystemRenderer>();

        // Some module properties can only be changed while stopped
        _snowSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        if (!wantSnow)
        {
            psr.enabled = false;
            return;
        }

        var main = _snowSystem.main;
        main.loop = true;
        main.prewarm = true;
        main.playOnAwake = false;
        main.duration = 5f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = marineSnowMaxParticles;
        main.startLifetime = new ParticleSystem.MinMaxCurve(6f, 10f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.035f);
        main.startColor = Color.white; // brightness is driven by the material colour
        main.gravityModifier = 0f;

        var emission = _snowSystem.emission;
        emission.enabled = true;
        emission.rateOverTime = marineSnowMaxParticles / 8f;

        float depth = SnowFarDistance - SnowNearDistance;
        var shape = _snowSystem.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.position = new Vector3(0f, 0f, SnowNearDistance + depth * 0.5f);
        shape.rotation = Vector3.zero;
        shape.scale = new Vector3(14f, 8f, depth);

        // Very gentle drift so specks hang in the water rather than streaming
        var noise = _snowSystem.noise;
        noise.enabled = true;
        noise.strength = 0.08f;
        noise.frequency = 0.25f;
        noise.scrollSpeed = 0.05f;
        noise.damping = true;

        var col = _snowSystem.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(1f, 0.85f), new GradientAlphaKey(0f, 1f) }
        );
        col.color = new ParticleSystem.MinMaxGradient(grad);

        EnsureSnowMaterial();
        psr.enabled = true;
        psr.sharedMaterial = _snowMaterial;
        psr.renderMode = ParticleSystemRenderMode.Billboard;
        psr.sortMode = ParticleSystemSortMode.None;
        psr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        psr.receiveShadows = false;
        psr.minParticleSize = 0f;
        psr.maxParticleSize = 0.012f; // hard cap: never more than ~1% of the screen

        UpdateMarineSnowAppearance();
        _snowSystem.Play();
    }

    private void EnsureSnowMaterial()
    {
        if (_snowMaterial != null) return;

        if (marineSnowMaterialOverride != null)
        {
            _snowMaterial = new Material(marineSnowMaterialOverride) { name = "Mat_MarineSnow (Instance)" };
            return;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        bool isUrpParticles = shader != null;
        if (shader == null) shader = Shader.Find("Sprites/Default");

        _snowMaterial = new Material(shader) { name = "Mat_MarineSnow" };
        Texture2D dot = GetSnowDotTexture();

        if (isUrpParticles)
        {
            // Transparent + Additive, configured the way URP's own material inspector would
            _snowMaterial.SetFloat("_Surface", 1f);   // Transparent
            _snowMaterial.SetFloat("_Blend", 2f);     // Additive
            _snowMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _snowMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            _snowMaterial.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            _snowMaterial.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            _snowMaterial.SetFloat("_ZWrite", 0f);
            _snowMaterial.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
            _snowMaterial.SetOverrideTag("RenderType", "Transparent");
            _snowMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            _snowMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            _snowMaterial.DisableKeyword("_ALPHAMODULATE_ON");

            // Camera fading: invisible within 2m, fully visible from 5m
            _snowMaterial.SetFloat("_CameraFadingEnabled", 1f);
            _snowMaterial.SetFloat("_CameraNearFadeDistance", SnowFadeStart);
            _snowMaterial.SetFloat("_CameraFarFadeDistance", SnowFadeEnd);
            _snowMaterial.SetVector("_CameraFadeParams", new Vector4(SnowFadeStart, 1f / (SnowFadeEnd - SnowFadeStart), 0f, 0f));
            _snowMaterial.SetFloat("_SoftParticlesEnabled", 0f);
            _snowMaterial.EnableKeyword("_FADING_ON");

            if (_snowMaterial.HasProperty("_BaseMap")) _snowMaterial.SetTexture("_BaseMap", dot);
        }

        if (_snowMaterial.HasProperty("_MainTex")) _snowMaterial.SetTexture("_MainTex", dot);
        _snowMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
    }

    private static Texture2D GetSnowDotTexture()
    {
        if (_snowDotTexture != null) return _snowDotTexture;

        const int size = 32;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Tex_MarineSnowDot",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        var pixels = new Color[size * size];
        float c = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c)) / c; // 0 centre, 1 edge
                float a = Mathf.Clamp01(1f - d);
                a = a * a; // soft round falloff
                pixels[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        }

        tex.SetPixels(pixels);
        tex.Apply(false, true);
        _snowDotTexture = tex;
        return _snowDotTexture;
    }

    private void UpdateMarineSnowAppearance()
    {
        if (_snowMaterial == null) return;

        Color litTint = Color.Lerp(CurrentTier.color, Color.white, 0.5f);
        Color darkTint = new Color(0.55f, 0.70f, 0.80f, 1f);

        Color c = Color.Lerp(darkTint, litTint, _lightBlend);
        c.a = Mathf.Lerp(snowBrightnessOff, snowBrightnessOn, _lightBlend);

        if (_snowMaterial.HasProperty("_BaseColor")) _snowMaterial.SetColor("_BaseColor", c);
        if (_snowMaterial.HasProperty("_Color")) _snowMaterial.SetColor("_Color", c);
    }

    // -----------------------------------------------------------------------
    // Lit-Water Fog Tint
    // -----------------------------------------------------------------------

    private void UpdateFogTint()
    {
        if (!RenderSettings.fog) return;

        Color current = RenderSettings.fogColor;

        // If someone else (ZoneManager) changed the fog colour, adopt it as the new base
        if (!_hasFogBase || !ColorsApproximatelyEqual(current, _lastWrittenFogColor))
        {
            _baseFogColor = current;
            _hasFogBase = true;
        }

        float amount = disableAllBeamEffects ? 0f : fogTintAmount * _lightBlend;
        Color target = Color.Lerp(_baseFogColor, CurrentTier.color, amount);
        target.a = 1f;

        if (!ColorsApproximatelyEqual(target, current))
        {
            RenderSettings.fogColor = target;

            // Background is the fog colour in this game; keep them matched
            if (_cam != null && _cam.clearFlags == CameraClearFlags.SolidColor)
            {
                _cam.backgroundColor = target;
            }
        }

        _lastWrittenFogColor = target;
    }

    private static bool ColorsApproximatelyEqual(Color a, Color b)
    {
        const float eps = 0.001f;
        return Mathf.Abs(a.r - b.r) < eps && Mathf.Abs(a.g - b.g) < eps && Mathf.Abs(a.b - b.b) < eps;
    }

    // -----------------------------------------------------------------------
    // Dynamic Fog Penetration System
    // -----------------------------------------------------------------------

    /// <summary>
    /// Updates RenderSettings fog distances based on the base zone values + light penetration bonus.
    /// </summary>
    public void ApplyFogPenetration(LightTierSettings settings, bool instantFog)
    {
        if (!RenderSettings.fog) return;

        float baseFogStart = ZoneManager.BaseFogStartDistance > 0f ? ZoneManager.BaseFogStartDistance : 20f;
        float baseFogEnd   = ZoneManager.BaseFogEndDistance > 0f   ? ZoneManager.BaseFogEndDistance   : 100f;

        float targetFogEnd   = baseFogEnd + (_isLightOn ? settings.fogEndBonus : 0f);
        float targetFogStart = baseFogStart + (_isLightOn ? settings.fogStartBonus : 0f);

        if (_fogBlendCoroutine != null)
        {
            StopCoroutine(_fogBlendCoroutine);
        }

        if (instantFog || !Application.isPlaying)
        {
            RenderSettings.fogStartDistance = targetFogStart;
            RenderSettings.fogEndDistance   = targetFogEnd;
            UpdateCameraFarClip(targetFogEnd);
        }
        else
        {
            _fogBlendCoroutine = StartCoroutine(BlendFogCoroutine(targetFogStart, targetFogEnd, 0.45f));
        }
    }

    private IEnumerator BlendFogCoroutine(float targetStart, float targetEnd, float duration)
    {
        float currentStart = RenderSettings.fogStartDistance;
        float currentEnd   = RenderSettings.fogEndDistance;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            RenderSettings.fogStartDistance = Mathf.Lerp(currentStart, targetStart, t);
            RenderSettings.fogEndDistance   = Mathf.Lerp(currentEnd, targetEnd, t);
            UpdateCameraFarClip(RenderSettings.fogEndDistance);
            yield return null;
        }

        RenderSettings.fogStartDistance = targetStart;
        RenderSettings.fogEndDistance   = targetEnd;
        UpdateCameraFarClip(targetEnd);
        _fogBlendCoroutine = null;
    }

    private void UpdateCameraFarClip(float fogEnd)
    {
        if (_cam == null)
        {
            _cam = GetComponent<Camera>();
            if (_cam == null) _cam = Camera.main;
        }
        if (_cam != null)
        {
            // Keep far clip slightly past fog end so geometry blends smoothly into dark water
            _cam.farClipPlane = Mathf.Max(fogEnd + 35f, 140f);
        }
    }

    /// <summary>
    /// Re-evaluates fog after a zone transition.
    /// </summary>
    public void ApplyCurrentAtmosphere()
    {
        ApplyFogPenetration(CurrentTier, instantFog: true);
    }

    // -----------------------------------------------------------------------
    // Procedural Auto-Setup for Submersible Light Rig
    // -----------------------------------------------------------------------

    /// <summary>
    /// Ensures all child lights exist and are calibrated to submarine cockpit dimensions.
    /// Can be called in Editor or at runtime.
    /// </summary>
    [ContextMenu("Rebuild Light Rig")]
    public void EnsureLightRigBuilt()
    {
        if (lightRigRoot == null)
        {
            Transform existing = transform.Find("SubmersibleLightRig");
            if (existing != null)
            {
                lightRigRoot = existing;
            }
            else
            {
                var rigGO = new GameObject("SubmersibleLightRig");
                rigGO.transform.SetParent(transform, false);
                rigGO.transform.localPosition = Vector3.zero;
                rigGO.transform.localRotation = Quaternion.identity;
                lightRigRoot = rigGO.transform;
            }
        }

        // Remove the old centre lamp and any camera-blocking beam geometry from earlier versions
        RemoveLegacyRigObjects();

        // 1. Port Spotlight (Boom offset Left: X = -0.75m, Y = -0.25m, Z = +0.45m), angled in so beams cross ~15m ahead
        if (portSpotlight == null)
        {
            portSpotlight = EnsureChildSpotlight("SpotLight_Port", new Vector3(-0.75f, -0.25f, 0.45f), Quaternion.Euler(0f, 2.8f, 0f));
        }

        // 2. Starboard Spotlight (Boom offset Right: X = +0.75m, Y = -0.25m, Z = +0.45m)
        if (starboardSpotlight == null)
        {
            starboardSpotlight = EnsureChildSpotlight("SpotLight_Starboard", new Vector3(0.75f, -0.25f, 0.45f), Quaternion.Euler(0f, -2.8f, 0f));
        }

        // 3. Cockpit Ambient Fill (Point light centered near viewport)
        if (cockpitFillLight == null)
        {
            Transform fillTr = lightRigRoot.Find("AmbientCockpitFill");
            GameObject fillGO = fillTr != null ? fillTr.gameObject : new GameObject("AmbientCockpitFill");
            fillGO.transform.SetParent(lightRigRoot, false);
            fillGO.transform.localPosition = new Vector3(0f, 0f, 0.1f);

            cockpitFillLight = fillGO.GetComponent<Light>();
            if (cockpitFillLight == null) cockpitFillLight = fillGO.AddComponent<Light>();

            cockpitFillLight.type = LightType.Point;
            cockpitFillLight.range = 6f;
            cockpitFillLight.intensity = 0.35f;
            cockpitFillLight.color = new Color(0.85f, 0.95f, 1.0f);
            cockpitFillLight.shadows = LightShadows.None;
        }

        EnsureMarineSnowVFX();

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EditorUtility.SetDirty(this);
        }
#endif
    }

    /// <summary>
    /// Destroys objects created by earlier rig versions: the centre spotlight, the volumetric
    /// cones and the lamp lens coronas (they rendered inside/over the camera and hid the screen).
    /// </summary>
    private void RemoveLegacyRigObjects()
    {
        if (lightRigRoot == null) return;

        var toRemove = new List<GameObject>();
        foreach (Transform t in lightRigRoot.GetComponentsInChildren<Transform>(true))
        {
            if (t == lightRigRoot) continue;
            string n = t.name;
            if (n == "SpotLight_Center" || n == "VolumetricBeamCone" || n == "LampLensCorona")
            {
                toRemove.Add(t.gameObject);
            }
        }

        foreach (var go in toRemove)
        {
            if (go == null) continue;
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }
    }

    private Light EnsureChildSpotlight(string lampName, Vector3 localPos, Quaternion localRot)
    {
        Transform lampTr = lightRigRoot.Find(lampName);
        GameObject lampGO = lampTr != null ? lampTr.gameObject : new GameObject(lampName);
        lampGO.transform.SetParent(lightRigRoot, false);
        lampGO.transform.localPosition = localPos;
        lampGO.transform.localRotation = localRot;

        Light l = lampGO.GetComponent<Light>();
        if (l == null) l = lampGO.AddComponent<Light>();

        l.type = LightType.Spot;
        l.shadows = LightShadows.Soft;
        l.shadowStrength = 0.85f;
        l.shadowNearPlane = 0.4f;

        return l;
    }

    // -----------------------------------------------------------------------
    // Gizmos Visualization
    // -----------------------------------------------------------------------

    private void OnDrawGizmosSelected()
    {
        LightTierSettings s = CurrentTier;

        Gizmos.color = _isLightOn ? s.color : Color.gray;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawFrustum(Vector3.zero, s.spotAngle, s.range, 0.5f, 1.33f);

        // Marine snow spawn volume
        Gizmos.color = new Color(0.8f, 0.95f, 1f, 0.25f);
        float depth = SnowFarDistance - SnowNearDistance;
        Gizmos.DrawWireCube(new Vector3(0f, 0f, SnowNearDistance + depth * 0.5f), new Vector3(14f, 8f, depth));

        // Draw fog boundary line in scene view
        if (RenderSettings.fog)
        {
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.35f);
            Gizmos.DrawWireSphere(Vector3.forward * RenderSettings.fogEndDistance, 10f);
        }
    }

    // -----------------------------------------------------------------------
    // Editor Menu Integration
    // -----------------------------------------------------------------------
#if UNITY_EDITOR
    [MenuItem("HadalDescent/Submarine/4. Setup Submarine Lights Rig")]
    public static void SetupLightsRigOnActivePlayer()
    {
        var cam = Camera.main;
        if (cam == null)
        {
            var subCam = FindFirstObjectByType<SubmarineCamera>();
            if (subCam != null) cam = subCam.GetComponent<Camera>();
        }

        if (cam == null)
        {
            Debug.LogError("[SubmarineLightController] Could not find Main Camera or SubmarineCamera in scene!");
            return;
        }

        var controller = cam.GetComponent<SubmarineLightController>();
        if (controller == null)
        {
            controller = cam.gameObject.AddComponent<SubmarineLightController>();
            Undo.RegisterCreatedObjectUndo(controller, "Add SubmarineLightController");
        }

        controller.EnsureLightRigBuilt();
        int tier = GameManager.Instance != null ? GameManager.Instance.LightTier : 1;
        controller.ApplyTier(tier, instantFog: true);

        EditorUtility.SetDirty(cam.gameObject);
        Debug.Log("<color=#2EE8C6>[SubmarineLightController] Successfully created & calibrated Submarine Lights Rig on Main Camera!</color>");
    }
#endif
}
