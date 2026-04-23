using UnityEditor;
using UnityEngine;

/// <summary>
/// Converts HandsDefaultMaterial from built-in Standard shader to URP Lit,
/// preserving the grey-transparent look. Runs automatically once per session.
/// </summary>
[InitializeOnLoad]
public static class FixHandMaterial
{
    private const string SessionKey = "HandMaterialFixed_v1";
    private const string MatPath    = "Assets/Samples/XR Hands/1.4.0/HandVisualizer/Materials/HandsDefaultMaterial.mat";

    static FixHandMaterial()
    {
        if (SessionState.GetBool(SessionKey, false)) return;
        SessionState.SetBool(SessionKey, true);
        EditorApplication.delayCall += Apply;
    }

    private static void Apply()
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(MatPath);
        if (mat == null)
        {
            Debug.LogWarning("[FixHandMaterial] HandsDefaultMaterial not found at " + MatPath);
            return;
        }

        // Already on a URP shader?
        if (mat.shader != null && mat.shader.name.Contains("Universal Render Pipeline"))
        {
            Debug.Log("[FixHandMaterial] Material already uses URP shader — nothing to do.");
            return;
        }

        var urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null)
        {
            Debug.LogWarning("[FixHandMaterial] Could not find 'Universal Render Pipeline/Lit' shader.");
            return;
        }

        // Read current colour before switching.
        Color col = mat.HasProperty("_BaseColor")
            ? mat.GetColor("_BaseColor")
            : new Color(0.665f, 0.665f, 0.665f, 0.65f);

        mat.shader = urpLit;

        // Restore semi-transparent grey.
        mat.SetColor("_BaseColor", col);
        mat.SetFloat("_Surface", 1f);   // 0 = Opaque, 1 = Transparent
        mat.SetFloat("_Blend",   0f);   // Alpha blend
        mat.SetFloat("_AlphaClip", 0f);
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        mat.SetOverrideTag("RenderType", "Transparent");

        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();

        Debug.Log("[FixHandMaterial] HandsDefaultMaterial upgraded to URP Lit (semi-transparent grey).");
    }
}
