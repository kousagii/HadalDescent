using System;
using UnityEditor;
using UnityEngine;

/// <summary>Checks native Unity imports and the camera effect's shader before device playtesting.</summary>
public static class HazardDodgeValidation
{
    [MenuItem("HadalDescent/Hazard Dodge/Validate Assets and Shader")]
    public static void ValidateAssetsAndShader()
    {
        var assets = Resources.Load<HazardDodgeVisualAssets>("HazardDodgeVisuals");
        if (assets == null || assets.rocks == null || assets.rocks.Length < 2 || assets.chimney == null)
            throw new InvalidOperationException("Hazard Dodge rock/vent references are missing.");
        foreach (var rock in assets.rocks)
            if (rock == null || rock.GetComponentsInChildren<Renderer>(true).Length == 0)
                throw new InvalidOperationException("A Hazard Dodge rock has no renderers.");
        if (assets.particles == null || assets.underwater == null)
            throw new InvalidOperationException("Hazard Dodge particle/water material is missing.");
        var shader = assets.underwater.shader;
        if (shader == null || shader.name != "HadalDescent/Hazard Underwater")
            throw new InvalidOperationException("Hazard Dodge water shader could not be imported.");
        ShaderUtil.CompilePass(assets.underwater,0,true);
        foreach (var message in ShaderUtil.GetShaderMessages(shader))
            if (message.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error)
                throw new InvalidOperationException(message.message);
        Debug.Log("[HazardDodgeValidation] Existing model references, materials and water shader validated. Check camera rendering and touch input in Play Mode and on Android.");
    }
}
