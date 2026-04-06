using UnityEngine;

public class ReelSpinner : MonoBehaviour
{
    [Header("Settings")]
    public float spinSpeed = 8f;

    private bool isSpinning = false;
    private float currentOffset = 0f;
    private Material reelMaterial;
    private string texturePropertyName = "_BaseMap";

    void Start()
    {
        Renderer rend = GetComponent<Renderer>();
        if (rend != null)
        {
            reelMaterial = rend.material;
            // Support for both URP (_BaseMap) and Standard (_MainTex) shaders
            if (!reelMaterial.HasProperty("_BaseMap") && reelMaterial.HasProperty("_MainTex"))
            {
                texturePropertyName = "_MainTex";
            }
        }
    }

    // This is the function your Lever will call
    public void ToggleSpin()
    {
        isSpinning = !isSpinning;

        if (!isSpinning)
        {
            // Snaps to the nearest icon (assuming 4 icons on the strip)
            currentOffset = Mathf.Round(currentOffset * 4f) / 4f;
            UpdateShader(currentOffset);
        }
    }

    void Update()
    {
        if (isSpinning)
        {
            currentOffset += Time.deltaTime * spinSpeed;
            UpdateShader(currentOffset);
        }
    }

    void UpdateShader(float offset)
    {
        if (reelMaterial != null)
        {
            // Note: If icons are vertical, use Vector2(0, offset) instead
            reelMaterial.SetTextureOffset(texturePropertyName, new Vector2(offset, 0));
        }
    }
}