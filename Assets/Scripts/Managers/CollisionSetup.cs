using UnityEngine;

/// <summary>
/// Configures the physics layer collision matrix at game startup.
/// 
/// Purpose:
///   - Prevents player submarine from physically colliding with creatures.
///   - Creatures still collide with terrain for ContextSteering raycasts.
///   - Acts as a safety net alongside trigger-based colliders on species.
///
/// This runs once via [RuntimeInitializeOnLoadMethod] before any scene loads.
/// </summary>
public static class CollisionSetup
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ConfigureCollisionMatrix()
    {
        int creaturesLayer = LayerMask.NameToLayer("Creatures");
        int playerLayer    = LayerMask.NameToLayer("Player");


        // Disable Creatures ↔ Player physical collisions
        if (playerLayer >= 0)
        {
            Physics.IgnoreLayerCollision(creaturesLayer, playerLayer, true);
            Debug.Log($"[CollisionSetup] Disabled collisions: Creatures (layer {creaturesLayer}) ↔ Player (layer {playerLayer})");
        }

        // Also disable Creatures ↔ Creatures (fish shouldn't block each other)
        Physics.IgnoreLayerCollision(creaturesLayer, creaturesLayer, true);

        Debug.Log("[CollisionSetup] Physics collision matrix configured.");
    }
}
