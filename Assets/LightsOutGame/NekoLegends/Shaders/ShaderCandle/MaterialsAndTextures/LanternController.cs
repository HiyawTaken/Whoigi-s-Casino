using UnityEngine;

[DisallowMultipleComponent]
public class LanternController : MonoBehaviour 
{
    public bool isLit;
    public GameObject pointLight; // Reference to the Point Light component
    public Material litMaterial;   // Material with emission
    public Material unlitMaterial; // Standard material

    private Renderer cachedRenderer;

    private void Awake()
    {
        RefreshVisualState();
    }

    public void ToggleLantern() 
    {
        SetLit(!isLit);
    }

    public void SetLit(bool lit)
    {
        isLit = lit;
        RefreshVisualState();
    }

    public void RefreshVisualState()
    {
        if (pointLight != null)
        {
            pointLight.SetActive(isLit);
        }

        Material targetMaterial = isLit ? litMaterial : unlitMaterial;
        if (targetMaterial == null)
        {
            return;
        }

        Renderer targetRenderer = GetTargetRenderer();
        if (targetRenderer == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            targetRenderer.material = targetMaterial;
        }
        else
        {
            targetRenderer.sharedMaterial = targetMaterial;
        }
    }

    private Renderer GetTargetRenderer()
    {
        if (cachedRenderer == null)
        {
            cachedRenderer = GetComponent<Renderer>();
            if (cachedRenderer == null)
            {
                cachedRenderer = GetComponentInChildren<Renderer>(true);
            }
        }

        return cachedRenderer;
    }
}
