using System;

/// <summary>Brief current feedback and suspended sediment, advanced only by the run clock.</summary>
public sealed class HazardDodgeConditions
{
    public int CurrentDirection { get; private set; }
    public float Pressure { get; private set; }
    public float Sediment { get; private set; }
    public float CloudTime { get; private set; }
    public float Clock { get; private set; }
    public bool InCurrent => _gustTime > 0f && Pressure > .1f;
    private float _gustTime, _gustStrength, _cloudDensity;

    public void BeginCurrent(int direction, float strength)
    {
        CurrentDirection = direction < 0 ? -1 : 1;
        Pressure = _gustStrength = Clamp(strength, .65f, 1.5f);
        _gustTime = .65f;
    }

    public void EnterSediment(float density)
    {
        _cloudDensity = Math.Max(_cloudDensity, Clamp(density, .45f, 1f));
        CloudTime = Math.Max(CloudTime, 2.4f + _cloudDensity * .8f);
        Sediment = Math.Max(Sediment, _cloudDensity * .55f);
    }

    public void Tick(float dt, float movement, float disturbance)
    {
        // Substeps keep buildup and settling consistent across mobile frame rates.
        float remaining = Clamp(dt, 0f, .2f);
        while (remaining > .00001f)
        {
            float step = Math.Min(remaining, 1f / 60f);
            remaining -= step;
            Clock += step;
            _gustTime = Math.Max(0f, _gustTime - step);
            Pressure = _gustStrength * Clamp01(_gustTime / .35f);

            float stir = Clamp01(movement) * .20f + Clamp01(disturbance) * .12f + Pressure * .06f;
            float target = CloudTime > 0f ? Math.Min(.94f, _cloudDensity * .60f + .16f + stir) : 0f;
            float response = target > Sediment ? 8f : (CloudTime > 0f ? 1.8f : 1.6f);
            Sediment += (target - Sediment) * (1f - (float)Math.Exp(-response * step));
            CloudTime = Math.Max(0f, CloudTime - step);
            if (CloudTime <= 0f) _cloudDensity = 0f;
            if (Sediment < .001f) Sediment = 0f;
        }
    }

    public void Reset()
    {
        CurrentDirection = 0;
        Pressure = Sediment = CloudTime = Clock = 0f;
        _gustTime = _gustStrength = _cloudDensity = 0f;
    }

    private static float Clamp(float value, float min, float max) => Math.Max(min, Math.Min(max, value));
    private static float Clamp01(float value) => Clamp(value, 0f, 1f);
}
