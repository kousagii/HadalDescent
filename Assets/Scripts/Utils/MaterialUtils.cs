using UnityEngine;

/// <summary>
/// Central utility for creating and obtaining materials compatible with URP in standalone mobile/desktop builds.
/// In standalone builds, Shader.Find() returns null for shaders not included in "Always Included Shaders".
/// This utility loads the pre-bundled "Materials/Default_URP_Lit" asset from Resources, ensuring reliable
/// URP Lit material creation across all build targets.
/// </summary>
public static class MaterialUtils
{
    private static Material _baseURPLit;

    /// <summary>
    /// Returns a new material instance configured with URP Lit and the given color and smoothness.
    /// </summary>
    public static Material CreateColoredMaterial(Color color, float smoothness = 0.2f, float metallic = 0.0f)
    {
        Material mat = CreateBaseMaterial();
        SetMaterialColor(mat, color);
        if (mat.HasProperty("_Smoothness"))
            mat.SetFloat("_Smoothness", smoothness);
        if (mat.HasProperty("_Metallic"))
            mat.SetFloat("_Metallic", metallic);
        return mat;
    }

    /// <summary>
    /// Instantiates a new Material based on Default_URP_Lit (from Resources) with fallbacks.
    /// </summary>
    public static Material CreateBaseMaterial()
    {
        if (_baseURPLit == null)
        {
            _baseURPLit = Resources.Load<Material>("Materials/Default_URP_Lit");
        }

        if (_baseURPLit != null)
        {
            return new Material(_baseURPLit);
        }

        // Fallback for editor or non-bundled testing
        Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                     ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                     ?? Shader.Find("Universal Render Pipeline/Unlit")
                     ?? Shader.Find("Standard")
                     ?? Shader.Find("Diffuse");

        if (shader != null)
        {
            return new Material(shader);
        }

        return new Material(Shader.Find("Hidden/InternalErrorShader") ?? Shader.Find("Standard"));
    }

    /// <summary>
    /// Reliably sets the color on both URP (_BaseColor) and legacy (_Color / color) properties.
    /// </summary>
    public static void SetMaterialColor(Material mat, Color color)
    {
        if (mat == null) return;
        mat.color = color;
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", color);
    }
}
