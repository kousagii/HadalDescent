using UnityEngine;

/// <summary>Converts between an imported model's axes and its travel heading.</summary>
public static class CreatureFacing
{
    public static Vector3 Forward(Quaternion modelRotation, float yawOffset)
        => modelRotation * Quaternion.Euler(0f, -yawOffset, 0f) * Vector3.forward;

    public static Quaternion ModelRotation(Quaternion travelRotation, float yawOffset)
        => travelRotation * Quaternion.Euler(0f, yawOffset, 0f);

    public static Bounds MovementBounds(Bounds modelBounds, float yawOffset)
    {
        Quaternion correction = Quaternion.Euler(0f, yawOffset, 0f);
        Bounds result = new Bounds(correction * modelBounds.center, Vector3.zero);
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = new Vector3((i & 1) == 0 ? modelBounds.min.x : modelBounds.max.x,
                (i & 2) == 0 ? modelBounds.min.y : modelBounds.max.y,
                (i & 4) == 0 ? modelBounds.min.z : modelBounds.max.z);
            result.Encapsulate(correction * corner);
        }
        return result;
    }
}
