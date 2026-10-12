using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Procedural VFX and Atmosphere for Zone 0 (Sunlight Zone):
///   1. Water Surface Plane (Y = 0m):
///      - Generates an infinite rolling ocean surface dividing water from open air.
///      - Double-sided geometry: glistening ocean plane from above, sunlit caustics ceiling from below.
///   2. Above-Water Atmosphere & Skybox Transition (Y >= 0m):
///      - Smoothly transitions from murky underwater fog (#2E84B8, 25-140m) to radiant sunny open sky
///        (#70B5F9, 140-700m clear visibility).
///      - Switches Camera clearFlags to daytime Skybox (Skybox_Daytime) with glowing sun disc and blue atmosphere.
///      - Shifting ambient light to warm sunny daylight.
///   3. Sunlit Godrays:
///      - Angled light shafts descending from the surface (Y=0 down into the deep) with gentle wave oscillation.
///      - Fades out dynamically when surfaced above water.
///   4. Marine Snow / Plankton:
///      - Ambient drifting particulate in water column; stops emitting when breaching into open air.
/// </summary>
public class SunlightAtmosphereVFX : MonoBehaviour
{
    private static SunlightAtmosphereVFX _instance;
    public static SunlightAtmosphereVFX Instance => _instance;

    [Header("Water Surface Settings (Y = 0m)")]
    [SerializeField] private float waterSize = 800f;
    [SerializeField] private int   waterGridResolution = 40;
    [SerializeField] private float waveSpeed = 1.25f;
    [SerializeField] private float waveHeight = 0.18f;
    [SerializeField] private float waveFrequency = 0.06f;
    [SerializeField] private Color waterSurfaceColor = new Color(0.12f, 0.56f, 0.76f, 0.70f);
    [SerializeField] private Color waterUndersideColor = new Color(0.25f, 0.72f, 0.95f, 0.20f);

    [Header("Surface / Sky Transition Settings")]
    [SerializeField] private float surfaceThresholdY = 0.0f;
    [SerializeField] private float transitionSpan = 0.5f;
    [SerializeField] private Color skyFogColor = new Color(0.62f, 0.82f, 0.98f, 1.0f);
    [SerializeField] private Color skyAmbientColor = new Color(0.96f, 0.95f, 0.90f, 1.0f);
    [SerializeField] private Color skyZenithColor = new Color(0.42f, 0.70f, 0.96f, 1.0f);
    [SerializeField] private float skyFogStart = 140f;
    [SerializeField] private float skyFogEnd = 700f;

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
    private Camera         _mainCam;
    private ParticleSystem _planktonPS;
    private GameObject     _godraysRoot;
    private Transform[]    _rayPillars;
    private float[]        _rayPhases;
    private Material       _rayMat;

    // Water surface runtime
    private GameObject     _waterPlaneGO;
    private Mesh           _waterMesh;
    private Vector3[]      _waterBaseVerts;
    private Vector3[]      _waterDisplacedVerts;
    private Material       _waterMat;

    // Skybox & Atmosphere transition runtime
    private Material       _skyboxMat;
    private Color          _underwaterFogColor = new Color(0.18f, 0.52f, 0.72f, 1f);
    private float          _underwaterFogStart = 25f;
    private float          _underwaterFogEnd   = 140f;
    private Color          _underwaterAmbient  = new Color(0.12f, 0.28f, 0.40f, 1f);
    private float          _currentBlend = 0f;

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
            if (_instance._planktonPS == null)   _instance.CreatePlanktonParticleSystem();
            if (_instance._godraysRoot == null)  _instance.CreateGodrayLightShafts();
            if (_instance._waterPlaneGO == null) _instance.CreateWaterSurfacePlane();
            return;
        }

        var existing = FindFirstObjectByType<SunlightAtmosphereVFX>();
        if (existing != null)
        {
            _instance = existing;
            _instance.FindPlayerSub();
            if (_instance._planktonPS == null)   _instance.CreatePlanktonParticleSystem();
            if (_instance._godraysRoot == null)  _instance.CreateGodrayLightShafts();
            if (_instance._waterPlaneGO == null) _instance.CreateWaterSurfacePlane();
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

        if (surfaceRayOpacity > 0.15f) surfaceRayOpacity = 0.08f;
        if (deepRayOpacity > 0.05f)    deepRayOpacity = 0.02f;
    }

    private void Start()
    {
        FindPlayerSub();
        CacheInitialAtmosphere();
        LoadSkyboxMaterial();
        CreateWaterSurfacePlane();
        CreatePlanktonParticleSystem();
        CreateGodrayLightShafts();
    }

    private void OnDestroy()
    {
        RestoreInitialAtmosphere();

        if (_waterPlaneGO != null) Destroy(_waterPlaneGO);
        if (_waterMesh != null) Destroy(_waterMesh);
        if (_waterMat != null) Destroy(_waterMat);

        if (_instance == this)
        {
            _instance = null;
        }
    }

    private void FindPlayerSub()
    {
        var pm = FindFirstObjectByType<PlayerMovement>();
        if (pm != null) _playerTransform = pm.transform;
        else if (Camera.main != null) _playerTransform = Camera.main.transform;

        if (_mainCam == null)
            _mainCam = Camera.main ?? FindFirstObjectByType<Camera>();
    }

    private void CacheInitialAtmosphere()
    {
        if (RenderSettings.fog)
        {
            _underwaterFogColor = RenderSettings.fogColor;
            _underwaterFogStart = RenderSettings.fogStartDistance > 0f ? RenderSettings.fogStartDistance : 25f;
            _underwaterFogEnd   = RenderSettings.fogEndDistance > 0f   ? RenderSettings.fogEndDistance   : 140f;
            _underwaterAmbient  = RenderSettings.ambientLight;
        }
    }

    private void LoadSkyboxMaterial()
    {
        _skyboxMat = Resources.Load<Material>("Materials/Skybox_Daytime");
        if (_skyboxMat == null)
        {
            _skyboxMat = Resources.Load<Material>("Skybox_Daytime");
        }
    }

    private void RestoreInitialAtmosphere()
    {
        if (!Application.isPlaying) return;

        RenderSettings.skybox           = null;
        RenderSettings.fogColor         = _underwaterFogColor;
        RenderSettings.fogStartDistance = _underwaterFogStart;
        RenderSettings.fogEndDistance   = _underwaterFogEnd;
        RenderSettings.ambientLight     = _underwaterAmbient;

        if (_mainCam != null && _mainCam.targetTexture == null)
        {
            _mainCam.clearFlags      = CameraClearFlags.SolidColor;
            _mainCam.backgroundColor = _underwaterFogColor;
            _mainCam.farClipPlane    = _underwaterFogEnd + 35f;
        }
    }

    private void Update()
    {
        if (_playerTransform == null)
        {
            FindPlayerSub();
            return;
        }

        // 1. Water surface follows player horizontally so the ocean is infinite
        if (_waterPlaneGO != null)
        {
            Vector3 pPos = _playerTransform.position;
            _waterPlaneGO.transform.position = new Vector3(pPos.x, surfaceThresholdY, pPos.z);
            AnimateWaterWaves();
        }

        // 2. Anchor plankton simulation around player submarine
        if (_planktonPS != null)
        {
            _planktonPS.transform.position = _playerTransform.position;
        }

        // 3. Horizontally follow player submarine so godrays always stream down overhead
        if (_godraysRoot != null)
        {
            Vector3 pPos = _playerTransform.position;
            Vector3 rootPos = _godraysRoot.transform.position;
            rootPos.x = Mathf.Lerp(rootPos.x, pPos.x, Time.deltaTime * 3.0f);
            rootPos.z = Mathf.Lerp(rootPos.z, pPos.z, Time.deltaTime * 3.0f);
            rootPos.y = rayTopY;
            _godraysRoot.transform.position = rootPos;
        }

        // 4. Dynamic Depth Opacity: Godrays fade in deep water AND vanish above water surface
        if (_rayMat != null)
        {
            float playerDepth = Mathf.Abs(_playerTransform.position.y);
            float depthFactor = Mathf.Clamp01(playerDepth / 150f);
            float depthAlpha = Mathf.Lerp(surfaceRayOpacity, deepRayOpacity, depthFactor);

            // Attenuate to 0 when surfaced in the open air
            depthAlpha *= Mathf.Clamp01(1f - _currentBlend);

            Color dynColor = new Color(rayColor.r, rayColor.g, rayColor.b, depthAlpha);
            _rayMat.SetColor("_BaseColor", dynColor);
            _rayMat.SetColor("_Color", dynColor);
        }

        // 5. Gentle caustic wave oscillation for godrays
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

    private void LateUpdate()
    {
        // Execute atmosphere and camera updates in LateUpdate so camera position is finalized
        UpdateAtmosphereTransition();
    }

    // -----------------------------------------------------------------------
    // Water Surface Plane Generation & Wave Displacement
    // -----------------------------------------------------------------------

    private void CreateWaterSurfacePlane()
    {
        if (_waterPlaneGO != null) return;

        _waterPlaneGO = new GameObject("OceanWaterSurface_Y0");
        _waterPlaneGO.transform.SetParent(transform, false);
        _waterPlaneGO.transform.position = new Vector3(0f, surfaceThresholdY, 0f);

        var mf = _waterPlaneGO.AddComponent<MeshFilter>();
        var mr = _waterPlaneGO.AddComponent<MeshRenderer>();

        _waterMesh = GenerateWaterMesh(waterSize, waterGridResolution);
        mf.sharedMesh = _waterMesh;

        _waterBaseVerts = _waterMesh.vertices;
        _waterDisplacedVerts = new Vector3[_waterBaseVerts.Length];
        System.Array.Copy(_waterBaseVerts, _waterDisplacedVerts, _waterBaseVerts.Length);

        // Attempt loading project material, fallback to procedural double-sided transparent material
        Material loadedMat = Resources.Load<Material>("Materials/OceanSurfaceWater");
        if (loadedMat == null) loadedMat = Resources.Load<Material>("OceanSurfaceWater");

        if (loadedMat != null)
        {
            _waterMat = Instantiate(loadedMat);
            _waterMat.name = "OceanSurfaceWater_Instance";
            _waterMat.SetColor("_BaseColor", waterSurfaceColor);
        }
        else
        {
            _waterMat = CreateDefaultWaterMaterial();
        }

        mr.sharedMaterial = _waterMat;
        mr.shadowCastingMode = ShadowCastingMode.Off;
        mr.receiveShadows = true;
    }

    private Mesh GenerateWaterMesh(float size, int res)
    {
        int vertsPerSide = (res + 1) * (res + 1);
        int totalVerts = vertsPerSide * 2; // Double sided: top and bottom

        Vector3[] verts   = new Vector3[totalVerts];
        Vector2[] uvs     = new Vector2[totalVerts];
        Vector3[] normals = new Vector3[totalVerts];
        Color[]   colors  = new Color[totalVerts];
        int[]     tris    = new int[res * res * 6 * 2];

        float halfSize = size * 0.5f;
        float step = size / res;

        // Top side (facing +Y, looking down from open air)
        for (int z = 0; z <= res; z++)
        {
            for (int x = 0; x <= res; x++)
            {
                int i = z * (res + 1) + x;
                float px = -halfSize + x * step;
                float pz = -halfSize + z * step;
                verts[i] = new Vector3(px, 0f, pz);
                uvs[i]   = new Vector2((float)x / res * 16f, (float)z / res * 16f);
                normals[i] = Vector3.up;
                colors[i]  = waterSurfaceColor;
            }
        }

        // Bottom side (facing -Y, looking up from underwater)
        int offset = vertsPerSide;
        for (int z = 0; z <= res; z++)
        {
            for (int x = 0; x <= res; x++)
            {
                int i = offset + z * (res + 1) + x;
                float px = -halfSize + x * step;
                float pz = -halfSize + z * step;
                verts[i] = new Vector3(px, 0f, pz);
                uvs[i]   = new Vector2((float)x / res * 16f, (float)z / res * 16f);
                normals[i] = Vector3.down;
                colors[i]  = waterUndersideColor;
            }
        }

        int t = 0;
        for (int z = 0; z < res; z++)
        {
            for (int x = 0; x < res; x++)
            {
                int bl = z * (res + 1) + x;
                int br = bl + 1;
                int tl = bl + (res + 1);
                int tr = tl + 1;

                // Top side quads
                tris[t++] = bl;
                tris[t++] = tl;
                tris[t++] = br;
                tris[t++] = br;
                tris[t++] = tl;
                tris[t++] = tr;

                // Bottom side quads
                int blB = offset + bl;
                int brB = offset + br;
                int tlB = offset + tl;
                int trB = offset + tr;

                tris[t++] = blB;
                tris[t++] = brB;
                tris[t++] = tlB;
                tris[t++] = brB;
                tris[t++] = trB;
                tris[t++] = tlB;
            }
        }

        Mesh mesh = new Mesh();
        mesh.name = "OceanSurfaceGrid";
        mesh.vertices  = verts;
        mesh.uv        = uvs;
        mesh.normals   = normals;
        mesh.colors    = colors;
        mesh.triangles = tris;
        mesh.RecalculateBounds();
        return mesh;
    }

    private void AnimateWaterWaves()
    {
        if (_waterMesh == null || _waterBaseVerts == null || _waterDisplacedVerts == null) return;

        float t = Time.time * waveSpeed;
        int half = _waterBaseVerts.Length / 2;

        for (int i = 0; i < half; i++)
        {
            Vector3 b = _waterBaseVerts[i];
            float wave = Mathf.Sin(b.x * waveFrequency + t) * waveHeight * 0.6f
                       + Mathf.Cos(b.z * (waveFrequency * 1.3f) + t * 0.85f) * waveHeight * 0.4f;

            _waterDisplacedVerts[i]        = new Vector3(b.x, wave, b.z);
            _waterDisplacedVerts[i + half] = new Vector3(b.x, wave, b.z);
        }

        _waterMesh.vertices = _waterDisplacedVerts;
    }

    private Material CreateDefaultWaterMaterial()
    {
        Shader s = Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Standard");

        Material mat = new Material(s);
        mat.name = "ProceduralOceanWaterMat";
        mat.SetFloat("_Surface", 1f); // Transparent
        mat.SetFloat("_Blend", 0f);   // Alpha blend
        mat.SetInt("_Cull", 0);       // Double-sided
        mat.SetFloat("_Smoothness", 0.94f);
        mat.SetFloat("_Metallic", 0.15f);
        mat.SetColor("_BaseColor", waterSurfaceColor);
        mat.renderQueue = (int)RenderQueue.Transparent - 10;
        return mat;
    }

    // -----------------------------------------------------------------------
    // Atmosphere & Sky Dynamic Height Transition
    // -----------------------------------------------------------------------

    private void UpdateAtmosphereTransition()
    {
        if (_mainCam == null)
        {
            _mainCam = Camera.main ?? FindFirstObjectByType<Camera>();
            if (_mainCam == null) return;
        }

        float camY = _mainCam.transform.position.y;

        // Calculate blend factor:
        // camY <= -transitionSpan * 0.5f  => 0.0 (Underwater)
        // camY >= +transitionSpan * 0.5f  => 1.0 (Surfaced / Open Sky)
        float targetBlend = Mathf.Clamp01(Mathf.InverseLerp(
            surfaceThresholdY - transitionSpan * 0.5f,
            surfaceThresholdY + transitionSpan * 0.5f,
            camY));

        _currentBlend = Mathf.MoveTowards(_currentBlend, targetBlend, Time.deltaTime * 4.0f);

        // 1. Fog Color, Distances, and Ambient Light
        Color curFogColor = Color.Lerp(_underwaterFogColor, skyFogColor, _currentBlend);
        float curFogStart = Mathf.Lerp(_underwaterFogStart, skyFogStart, _currentBlend);
        float curFogEnd   = Mathf.Lerp(_underwaterFogEnd, skyFogEnd, _currentBlend);
        Color curAmbient  = Color.Lerp(_underwaterAmbient, skyAmbientColor, _currentBlend);

        RenderSettings.fogColor         = curFogColor;
        RenderSettings.fogStartDistance = curFogStart;
        RenderSettings.fogEndDistance   = curFogEnd;
        RenderSettings.ambientLight     = curAmbient;

        // 2. Camera Background / Skybox
        if (_mainCam.targetTexture == null)
        {
            _mainCam.farClipPlane = Mathf.Lerp(_underwaterFogEnd + 35f, skyFogEnd + 50f, _currentBlend);

            if (camY >= surfaceThresholdY)
            {
                // Surfaced into the open atmosphere!
                if (_skyboxMat != null)
                {
                    RenderSettings.skybox = _skyboxMat;
                    _mainCam.clearFlags   = CameraClearFlags.Skybox;
                }
                else
                {
                    _mainCam.clearFlags      = CameraClearFlags.SolidColor;
                    _mainCam.backgroundColor = Color.Lerp(_underwaterFogColor, skyZenithColor, _currentBlend);
                }
            }
            else
            {
                // Submerged in the ocean
                _mainCam.clearFlags      = CameraClearFlags.SolidColor;
                _mainCam.backgroundColor = curFogColor;
            }
        }

        // 3. Suppress marine snow / plankton in the open air
        if (_planktonPS != null)
        {
            var emission = _planktonPS.emission;
            emission.rateOverTime = spawnRate * (1f - _currentBlend);
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

        Texture2D glowTex = CreateSoftParticleTexture();
        Material mat = CreateAdditiveMaterial("Mat_Sun_Plankton", new Color(0.85f, 0.98f, 1.0f, 0.85f));
        mat.mainTexture = glowTex;
        mat.SetTexture("_BaseMap", glowTex);
        mat.SetTexture("_MainTex", glowTex);
        psr.material = mat;

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

        var emission = _planktonPS.emission;
        emission.rateOverTime = spawnRate;

        var shape = _planktonPS.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius    = sphereRadius;

        var vol = _planktonPS.velocityOverLifetime;
        vol.enabled = true;
        vol.x = new ParticleSystem.MinMaxCurve(-0.06f, 0.06f);
        vol.y = new ParticleSystem.MinMaxCurve(-0.14f, -0.03f);
        vol.z = new ParticleSystem.MinMaxCurve(-0.06f, 0.06f);

        var noise = _planktonPS.noise;
        noise.enabled   = true;
        noise.strength  = 0.40f;
        noise.frequency = 0.25f;
        noise.scrollSpeed = 0.18f;

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
        int segments = 8;
        int rings = 5;
        int vertCount = segments * rings;
        Vector3[] vertices = new Vector3[vertCount];
        Color[]   colors   = new Color[vertCount];
        int[]     triangles = new int[segments * (rings - 1) * 6];

        float[] ringY      = new float[] { 0.0f, -0.18f, -0.48f, -0.78f, -1.0f };
        float[] ringRadii  = new float[] { 0.35f, 0.65f, 1.15f, 1.65f, 2.10f };
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
        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 1f);
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
                a = a * a * (3f - 2f * a);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        }
        tex.Apply();
        return tex;
    }
}
