using System.Collections;
using UnityEngine;

/// <summary>
/// Expanding ring VFX played on stationary species when the Interact button is pressed.
/// Uses a LineRenderer circle that scales outward and fades to transparent.
///
/// Usage: SonarPulseVFX.PlayAt(worldPosition, radius, duration);
/// No scene setup required — effect GameObjects are spawned and self-destroy.
/// </summary>
public class SonarPulseVFX : MonoBehaviour
{
    private static readonly Color PulseColor = new Color(0f, 0.93f, 0.85f, 1f);
    private const int CircleSegments = 48;

    // -----------------------------------------------------------------------
    // Static factory
    // -----------------------------------------------------------------------

    /// <summary>
    /// Spawn a sonar pulse ring at <paramref name="worldPos"/> and animate it.
    /// </summary>
    public static void PlayAt(Vector3 worldPos, float radius = 2f, float duration = 0.9f)
    {
        var go  = new GameObject("SonarPulse");
        go.transform.position = worldPos;
        var vfx = go.AddComponent<SonarPulseVFX>();
        vfx.StartCoroutine(vfx.Animate(radius, duration));
    }

    // -----------------------------------------------------------------------
    // Animation coroutine
    // -----------------------------------------------------------------------

    private IEnumerator Animate(float maxRadius, float duration)
    {
        // Build LineRenderer circle
        var lr = gameObject.AddComponent<LineRenderer>();
        lr.loop          = true;
        lr.positionCount = CircleSegments + 1;
        lr.startWidth    = 0.18f;
        lr.endWidth      = 0.18f;
        lr.useWorldSpace = false;

        var mat = new Material(Shader.Find("Sprites/Default"));
        lr.material    = mat;
        lr.startColor  = PulseColor;
        lr.endColor    = PulseColor;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t      = elapsed / duration;
            float r      = Mathf.Lerp(0f, maxRadius, t);
            float alpha  = Mathf.Lerp(1f, 0f, t);

            // Update circle points
            for (int i = 0; i <= CircleSegments; i++)
            {
                float angle = i * (2f * Mathf.PI / CircleSegments);
                lr.SetPosition(i, new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r));
            }

            Color c = new Color(PulseColor.r, PulseColor.g, PulseColor.b, alpha);
            lr.startColor = c;
            lr.endColor   = c;

            yield return null;
        }

        Destroy(gameObject);
    }
}
