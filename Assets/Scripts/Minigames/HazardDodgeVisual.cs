using UnityEngine;
using Kind = HazardDodgeMinigame.HazardType;

/// <summary>Run-owned visuals composed from existing rocks and small particle systems.</summary>
public class HazardDodgeVisual : MonoBehaviour
{
    private Transform _fallingRock;
    private Transform _marker;
    private ParticleSystem _particles;
    private Material _rockMaterial;
    private Kind _kind;
    private float _intensity;
    private int _direction;

    public static HazardDodgeVisual Create(Transform parent, Vector3 position, Kind kind,
        float laneWidth, int direction, float intensity, HazardDodgeVisualAssets assets, Material particles,
        Material markerMaterial, GameObject overridePrefab = null)
    {
        var root = new GameObject(kind.ToString());
        root.transform.SetParent(parent, false);
        root.transform.position = position;
        var visual = root.AddComponent<HazardDodgeVisual>();
        visual._kind = kind;
        visual._intensity = intensity;
        visual._direction = direction;
        bool vent = kind == Kind.ThermalVent;
        bool rock = kind == Kind.RockOutcrop || kind == Kind.FallingRock;
        float width = Mathf.Min(laneWidth * (vent ? .30f : .55f), vent ? 1.8f : 3.2f);
        float plumeHeight = .8f;
        if (rock || vent)
        {
            GameObject prefab = overridePrefab;
            if (prefab == null && assets != null)
            {
                if (vent) prefab = assets.chimney;
                else if (assets.rocks != null && assets.rocks.Length > 0)
                    prefab = assets.rocks[Random.Range(0, assets.rocks.Length)];
            }
            var body = prefab != null ? Instantiate(prefab, root.transform) : GameObject.CreatePrimitive(PrimitiveType.Sphere);
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = Vector3.zero;
            if (!vent && overridePrefab == null) body.transform.Rotate(Vector3.up, Random.Range(0f, 360f), Space.World);
            if (CreatureGeometry.TryMeasure(body.transform, body.transform.rotation, out Bounds bounds))
            {
                float scale = width / Mathf.Max(.01f, Mathf.Max(bounds.size.x, bounds.size.z));
                body.transform.localScale *= scale;
                body.transform.localPosition = new Vector3(-bounds.center.x * scale,
                    -.6f - bounds.min.y * scale, -bounds.center.z * scale);
                plumeHeight = -.6f + bounds.size.y * scale;
            }
            foreach (var collider in body.GetComponentsInChildren<Collider>()) collider.enabled = false;
            foreach (var rb in body.GetComponentsInChildren<Rigidbody>()) rb.isKinematic = true;
            if (overridePrefab == null)
            {
                visual._rockMaterial = MaterialUtils.CreateColoredMaterial(vent ? new Color(.17f, .19f, .19f) : new Color(.31f, .29f, .25f));
                foreach (var renderer in body.GetComponentsInChildren<Renderer>()) renderer.sharedMaterial = visual._rockMaterial;
            }
            if (kind == Kind.FallingRock)
            {
                var fall = new GameObject("RockfallHeight");
                fall.transform.SetParent(root.transform, false);
                body.transform.SetParent(fall.transform, false);
                visual._fallingRock = fall.transform;
            }
        }

        Color markerColor = rock || vent ? new Color(1f,.65f,.18f) :
            kind == Kind.SedimentCloud ? new Color(.76f,.57f,.30f,.55f) : new Color(.25f,1f,1f);
        if (kind != Kind.CurrentSurge)
        {
            var marker = new GameObject("HazardFootprint", typeof(LineRenderer));
            marker.transform.SetParent(root.transform, false);
            var line = marker.GetComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.sharedMaterial = markerMaterial;
            line.widthMultiplier = .075f;
            line.positionCount = 4;
            line.loop = true;
            float half = Mathf.Min(laneWidth * .35f, 2.1f);
            line.SetPositions(new[] { new Vector3(-half,-.48f,-1.6f),new Vector3(-half,-.48f,1.6f),
                new Vector3(half,-.48f,1.6f),new Vector3(half,-.48f,-1.6f) });
            line.startColor = line.endColor = markerColor;
            visual._marker = marker.transform;
        }

        if (kind == Kind.CurrentSurge)
        {
            // A readable chevron marks the push direction alongside its flow particles.
            var arrow = new GameObject("FlowChevron", typeof(LineRenderer));
            arrow.transform.SetParent(root.transform,false);
            var cue = arrow.GetComponent<LineRenderer>();
            cue.sharedMaterial = markerMaterial;
            cue.useWorldSpace = false;
            cue.widthMultiplier = .28f;
            cue.numCornerVertices = 4;
            cue.numCapVertices = 3;
            cue.positionCount = 3;
            cue.startColor = cue.endColor = markerColor;
            float span = Mathf.Min(laneWidth * .30f, 1.8f);
            cue.SetPositions(new[] { new Vector3(-direction*span*.6f,.15f,1.25f),new Vector3(direction*span*.6f,.15f,0),
                new Vector3(-direction*span*.6f,.15f,-1.25f) });
            visual._marker = arrow.transform;
        }

        if (vent || kind == Kind.SedimentCloud || kind == Kind.CurrentSurge)
        {
            bool sediment = kind == Kind.SedimentCloud;
            bool current = kind == Kind.CurrentSurge;
            var effect = new GameObject("FlowParticles", typeof(ParticleSystem));
            effect.transform.SetParent(root.transform, false);
            effect.transform.localPosition = new Vector3(0f, vent ? plumeHeight : current ? .3f : .8f, 0f);
            visual._particles = effect.GetComponent<ParticleSystem>();
            visual._particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = visual._particles.main;
            main.useUnscaledTime = true;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = sediment ? 96 : current ? 36 : 32;
            main.prewarm = sediment || current;
            main.startLifetime = new ParticleSystem.MinMaxCurve(current ? .4f : vent ? .9f : 2f,
                current ? .75f : vent ? 1.8f : 3f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(current ? .07f : vent ? .3f : 1.8f,
                current ? .16f : sediment ? 3.6f : .65f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            var tint = current ? new Color(.30f,.90f,1f,.72f) :
                vent ? new Color(.4f,.44f,.45f,.55f) : new Color(.68f,.52f,.29f,.82f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                sediment ? new Color(.40f,.29f,.15f,.68f) : tint * .7f, tint);
            var emission = visual._particles.emission;
            emission.rateOverTime = (sediment ? 40f : current ? 38f : 16f) * intensity;
            var shape = visual._particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(vent ? .65f : current ? Mathf.Min(laneWidth * .45f, 3f) : laneWidth * .65f,
                current ? .4f : vent ? .2f : 1.6f, current ? 2.6f : vent ? .65f : 3.8f);
            var velocity = visual._particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = current ? direction * 5f : .18f;
            velocity.y = current ? .03f : vent ? 2f : .45f;
            velocity.z = current ? 0f : .1f;
            var noise = visual._particles.noise;
            noise.enabled = true;
            noise.strength = current ? .08f : sediment ? .50f : .35f;
            noise.frequency = .5f;
            noise.scrollSpeed = .2f;
            noise.quality = ParticleSystemNoiseQuality.Low;
            var size = visual._particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f,.45f),new Keyframe(.4f,1f),new Keyframe(1f,0f)));
            var fade = visual._particles.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white,0f),new GradientColorKey(Color.white,1f) },
                new[] { new GradientAlphaKey(0f,0f),new GradientAlphaKey(1f,.15f),new GradientAlphaKey(.8f,.7f),new GradientAlphaKey(0f,1f) });
            fade.color = gradient;
            var renderer = effect.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = particles;
            if (current)
            {
                renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.lengthScale = 2f;
                renderer.velocityScale = .12f;
            }
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            visual._particles.Play();
        }
        return visual;
    }

    public void Tick(float timeToSubmarine, float disturbance = 0f, float current = 0f)
    {
        if (_particles != null && _kind == Kind.SedimentCloud)
        {
            var emission = _particles.emission;
            emission.rateOverTime = 40f * _intensity * (1f + disturbance * .7f + current * .2f);
            var velocity = _particles.velocityOverLifetime;
            velocity.x = .18f + _direction * current * .6f;
            velocity.y = .2f + disturbance * .6f;
        }
        if (_fallingRock != null)
            _fallingRock.localPosition = Vector3.up * (6f * Mathf.Clamp01((timeToSubmarine - .12f) / .8f));
        if (_marker != null)
        {
            float pulse = .94f + Mathf.Sin(Time.unscaledTime * 5f) * .06f;
            _marker.localScale = new Vector3(pulse,1f,pulse);
        }
    }

    public void SetPaused(bool paused)
    {
        if (_particles == null) return;
        if (paused) _particles.Pause();
        else _particles.Play();
    }

    private void OnDestroy()
    {
        if (_rockMaterial != null) Destroy(_rockMaterial);
    }
}
