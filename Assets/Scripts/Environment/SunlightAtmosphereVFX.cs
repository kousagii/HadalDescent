using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Procedural VFX for Zone 0 (Sunlight Zone):
///   1. Sunlit Godrays: Angled light shafts descending from the surface (Y=0 down to Y=-60m)
///      with gentle caustic wave oscillation.
///   2. Plankton / Marine Snow: Floating particulate dust simulating in a sphere around the player
///      using Unity's ParticleSystem with gentle noise drift.
/// </summary>
public class SunlightAtmosphereVFX : MonoBehaviour
{
    private static SunlightAtmosphereVFX _instance;
    public static SunlightAtmosphereVFX Instance => _instance;

    [Header("Godrays Settings")]
    [SerializeField] private int   godrayCount = 10;
    [SerializeField] private float rayTopY = 20f;
    [SerializeField] private float rayBottomY = -80f;
    [SerializeField] private Color rayColor = new Color(0.70f, 0.95f, 1.0f, 0.08f);
    [Tooltip("Godray opacity near the water surface (0m - 30m depth)")]
    [SerializeField] private float surfaceRayOpacity = 0.08f;
    [Tooltip("Godray opacity in deep water near seabed (150m - 200m depth)")]
    [SerializeField] private float deepRayOpacity = 0.02f;

    [Header("Plankton Particle Settings")]
    [SerializeField] private int   maxParticles = 400;
    [SerializeField] private float spawnRate = 50f;
    [SerializeField] private float sphereRadius = 22f;

    private Transform      _playerTransform;
    private ParticleSystem _planktonPS;
    private GameObject     _godraysRoot;
    private Transform[]    _rayPillars;
    private float[]        _rayPhases;
    private Material       _rayMat;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InitSceneListener()
    {
        SceneManager.sceneLoaded += OnGlobalSceneLoaded;
    }

    private static void OnGlobalSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        bool isSunlight = scene.name.Contains("Sunlight");

        if (isSunlight)
        {
            EnsureInstance();
        }
        else if (_instance != null)
        {
            Destroy(_instance.gameObject);
            _instance = null;
        }
    }

    public static void EnsureInstance()
    {
        if (!SceneManager.GetActiveScene().name.Contains("Sunlight"))
        {
            if (_instance != null)
            {
                Destroy(_instance.gameObject);
                _instance = null;
            }
            return;
        }

        if (_instance != null)
        {
            _instance.gameObject.SetActive(true);
            _instance.FindPlayerSub();
            if (_instance._planktonPS == null) _instance.CreatePlanktonParticleSystem();
            if (_instance._godraysRoot == null) _instance.CreateGodrayLightShafts();
            return;
        }

        var existing = FindFirstObjectByType<SunlightAtmosphereVFX>();
        if (existing != null)
        {
            _instance = existing;
            _instance.FindPlayerSub();
            if (_instance._planktonPS == null) _instance.CreatePlanktonParticleSystem();
            if (_instance._godraysRoot == null) _instance.CreateGodrayLightShafts();
            return;
        }

        var go = new GameObject("SunlightAtmosphereVFX");
        _instance = go.AddComponent<SunlightAtmosphereVFX>();
    }

    private void Awake()
    {
        if (!SceneManager.GetActiveScene().name.Contains("Sunlight"))
        {
            Destroy(gameObject);
            return;
        }

        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;

        // Ensure rays remain soft and ethereal (never blinding) even with old serialized scene values
        if (surfaceRayOpacity > 0.15f) surfaceRayOpacity = 0.08f;
        if (deepRayOpacity > 0.05f)    deepRayOpacity = 0.02f;
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    private void Start()
    {
        FindPlayerSub();
        CreatePlanktonParticleSystem();
        CreateGodrayLightShafts();
    }

    private void FindPlayerSub()
    {
        var pm = FindFirstObjectByType<PlayerMovement>();
        if (pm != null) _playerTransform = pm.transform;
        else if (Camera.main != null) _playerTransform = Camera.main.transform;
    }

    private void Update()
    {
        if (_playerTransform == null)
        {
            FindPlayerSub();
            return;
        }

        // 1. Anchor plankton simulation around player submarine
        if (_planktonPS != null)
        {
            _planktonPS.transform.position = _playerTransform.position;
        }

        // 2. Horizontally follow player submarine so godrays always stream down overhead
        if (_godraysRoot != null)
        {
            Vector3 pPos = _playerTransform.position;
            Vector3 rootPos = _godraysRoot.transform.position;
            rootPos.x = Mathf.Lerp(rootPos.x, pPos.x, Time.deltaTime * 3.0f);
            rootPos.z = Mathf.Lerp(rootPos.z, pPos.z, Time.deltaTime * 3.0f);
            rootPos.y = rayTopY;
            _godraysRoot.transform.position = rootPos;
        }

        // 3. Dynamic Depth Opacity: Sunlight rays fade gracefully the deeper the player descends
        if (_rayMat != null)
        {
            float playerDepth = Mathf.Abs(_playerTransform.position.y);
            float depthFactor = Mathf.Clamp01(playerDepth / 150f);
            // Smooth natural attenuation from bright surface to subtle deep glow
            float depthAlpha = Mathf.Lerp(surfaceRayOpacity, deepRayOpacity, depthFactor);
            Color dynColor = new Color(rayColor.r, rayColor.g, rayColor.b, depthAlpha);
            _rayMat.SetColor("_BaseColor", dynColor);
            _rayMat.SetColor("_Color", dynColor);
        }

        // 4. Gentle caustic wave oscillation for godrays
        if (_rayPillars != null)
        {
            float t = Time.time;
            for (int i = 0; i < _rayPillars.Length; i++)
            {
                if (_rayPillars[i] == null) continue;
                float sway = Mathf.Sin(t * 0.55f + _rayPhases[i]) * 2.2f;
                float tilt = Mathf.Cos(t * 0.40f + _rayPhases[i]) * 1.8f;
                _rayPillars[i].localRotation = Quaternion.Euler(14f + tilt, _rayPhases[i] * Mathf.Rad2Deg + sway, 0f);
            }
        }
    }

    // -----------------------------------------------------------------------
    // Plankton / Marine Snow Particles
    // -----------------------------------------------------------------------

    private void CreatePlanktonParticleSystem()
    {
        if (_planktonPS != null) return;

        var psGO = new GameObject("PlanktonParticles");
        psGO.transform.SetParent(transform, false);
        if (_playerTransform != null) psGO.transform.position = _playerTransform.position;

        _planktonPS = psGO.AddComponent<ParticleSystem>();
        var psr = psGO.GetComponent<ParticleSystemRenderer>();

        // Material: soft additive particle material with procedural circular glow texture
        Texture2D glowTex = CreateSoftParticleTexture();
        Material mat = CreateAdditiveMaterial("Mat_Sun_Plankton", new Color(0.85f, 0.98f, 1.0f, 0.85f));
        mat.mainTexture = glowTex;
        mat.SetTexture("_BaseMap", glowTex);
        mat.SetTexture("_MainTex", glowTex);
        psr.material = mat;

        // Main Module
        var main = _planktonPS.main;
        main.maxParticles         = maxParticles;
        main.startLifetime        = new ParticleSystem.MinMaxCurve(5f, 9f);
        main.startSpeed           = new ParticleSystem.MinMaxCurve(0.08f, 0.28f);
        main.startSize            = new ParticleSystem.MinMaxCurve(0.18f, 0.36f);
        main.startColor           = new ParticleSystem.MinMaxGradient(
            new Color(0.85f, 0.98f, 1.0f, 0.75f),
            new Color(0.65f, 0.95f, 0.85f, 0.85f));
        main.simulationSpace      = ParticleSystemSimulationSpace.World;
        main.playOnAwake          = true;
        main.loop                 = true;

        // Emission Module
        var emission = _planktonPS.emission;
        emission.rateOverTime = spawnRate;

        // Shape Module: sphere surrounding player
        var shape = _planktonPS.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius    = sphereRadius;

        // Velocity over Lifetime: gentle downward drift
        var vol = _planktonPS.velocityOverLifetime;
        vol.enabled = true;
        vol.x = new ParticleSystem.MinMaxCurve(-0.06f, 0.06f);
        vol.y = new ParticleSystem.MinMaxCurve(-0.14f, -0.03f);
        vol.z = new ParticleSystem.MinMaxCurve(-0.06f, 0.06f);

        // Noise Module: fluid organic turbulence
        var noise = _planktonPS.noise;
        noise.enabled   = true;
        noise.strength  = 0.40f;
        noise.frequency = 0.25f;
        noise.scrollSpeed = 0.18f;

        // Color over Lifetime: smooth fade in and fade out
        var col = _planktonPS.colorOverLifetime;
        col.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(0.9f, 0.25f), new GradientAlphaKey(0.9f, 0.75f), new GradientAlphaKey(0f, 1f) }
        );
        col.color = grad;

        _planktonPS.Play();
    }

    // -----------------------------------------------------------------------
    // Procedural Godrays (Volumetric Light Shafts)
    // -----------------------------------------------------------------------

    private void CreateGodrayLightShafts()
    {
        if (_godraysRoot != null) return;

        _godraysRoot = new GameObject("SunlightGodrays");
        _godraysRoot.transform.SetParent(transform, false);

        Vector3 spawnCenter = _playerTransform != null ? _playerTransform.position : Vector3.zero;
        _godraysRoot.transform.position = new Vector3(spawnCenter.x, rayTopY, spawnCenter.z);

        Material rayMat = CreateAdditiveMaterial("Mat_Sun_Godrays", rayColor);
        _rayMat = rayMat;

        Mesh rayMesh = CreateGodrayMesh();

        _rayPillars = new Transform[godrayCount];
        _rayPhases  = new float[godrayCount];

        for (int i = 0; i < godrayCount; i++)
        {
            var shaftGO = new GameObject($"Godray_{i:00}");
            shaftGO.transform.SetParent(_godraysRoot.transform, false);

            float angle = (i / (float)godrayCount) * Mathf.PI * 2f;
            float dist  = Random.Range(12f, 60f);
            float x     = Mathf.Cos(angle) * dist + Random.Range(-8f, 8f);
            float z     = Mathf.Sin(angle) * dist + Random.Range(-8f, 8f);
            shaftGO.transform.localPosition = new Vector3(x, 0f, z);

            var mf = shaftGO.AddComponent<MeshFilter>();
            mf.sharedMesh = rayMesh;
            var mr = shaftGO.AddComponent<MeshRenderer>();
            mr.sharedMaterial = rayMat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows    = false;

            float widthScale = Random.Range(8f, 18f);
            float heightScale = Mathf.Abs(rayTopY - rayBottomY);
            shaftGO.transform.localScale = new Vector3(widthScale, heightScale, widthScale);

            _rayPillars[i] = shaftGO.transform;
            _rayPhases[i]  = Random.Range(0f, Mathf.PI * 2f);
        }
    }

    private Mesh CreateGodrayMesh()
    {
        // 8-sided tapered volumetric cylinder/cone with smooth vertical falloff
        int segments = 8;
        int rings = 5;
        int vertCount = segments * rings;
        Vector3[] vertices = new Vector3[vertCount];
        Color[]   colors   = new Color[vertCount];
        int[]     triangles = new int[segments * (rings - 1) * 6];

        float[] ringY      = new float[] { 0.0f, -0.18f, -0.48f, -0.78f, -1.0f };
        float[] ringRadii  = new float[] { 0.35f, 0.65f, 1.15f, 1.65f, 2.10f };
        // Gentle, translucent falloff (max 0.35 alpha on topmost rim instead of 1.0)
        float[] ringAlphas = new float[] { 0.35f, 0.28f, 0.16f, 0.06f, 0.01f };

        for (int r = 0; r < rings; r++)
        {
            float y = ringY[r];
            float rad = ringRadii[r];
            float a = ringAlphas[r];

            for (int s = 0; s < segments; s++)
            {
                float angle = (s / (float)segments) * Mathf.PI * 2f;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);

                int idx = r * segments + s;
                vertices[idx] = new Vector3(cos * rad, y, sin * rad);
                colors[idx]   = new Color(1f, 1f, 1f, a);
            }
        }

        int ti = 0;
        for (int r = 0; r < rings - 1; r++)
        {
            int rStart = r * segments;
            int nextRStart = (r + 1) * segments;

            for (int s = 0; s < segments; s++)
            {
                int nextS = (s + 1) % segments;
                int topL = rStart + s;
                int topR = rStart + nextS;
                int botL = nextRStart + s;
                int botR = nextRStart + nextS;

                // Double sided quads so rays look full from all viewing angles
                triangles[ti++] = topL; triangles[ti++] = botL; triangles[ti++] = topR;
                triangles[ti++] = topR; triangles[ti++] = botL; triangles[ti++] = botR;
            }
        }

        Mesh mesh = new Mesh();
        mesh.name = "ProceduralGodrayCone";
        mesh.vertices  = vertices;
        mesh.colors    = colors;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Material CreateAdditiveMaterial(string name, Color color)
    {
        Shader s = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                ?? Shader.Find("Particles/Standard Unlit")
                ?? Shader.Find("Mobile/Particles/Additive")
                ?? Shader.Find("Legacy Shaders/Particles/Additive")
                ?? Shader.Find("Sprites/Default");

        Material mat = new Material(s);
        mat.name = name;
        mat.SetFloat("_Surface", 1f); // Transparent
        mat.SetFloat("_Blend", 1f);   // Additive
        mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)BlendMode.One);
        mat.SetInt("_ZWrite", 0);
        mat.renderQueue = (int)RenderQueue.Transparent + 100;

        mat.SetColor("_BaseColor", color);
        mat.SetColor("_Color", color);
        if (mat.HasProperty("_Color")) mat.color = color;

        return mat;
    }

    private static Texture2D CreateSoftParticleTexture()
    {
        int res = 32;
        Texture2D tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
        tex.name = "SoftGlowParticle";
        tex.wrapMode = TextureWrapMode.Clamp;
        float center = (res - 1) * 0.5f;
        float maxR = center;

        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                float dx = (x - center) / maxR;
                float dy = (y - center) / maxR;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - d);
                a = a * a * (3f - 2f * a); // Smoothstep falloff
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        return tex;
    }
}
