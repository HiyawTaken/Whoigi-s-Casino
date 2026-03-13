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
            // Check for URP or Standard shader
            if (!reelMaterial.HasProperty("_BaseMap") && reelMaterial.HasProperty("_MainTex"))
            {
                texturePropertyName = "_MainTex";
            }
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            isSpinning = !isSpinning;

            if (!isSpinning)
            {
                // Snap to nearest 0.25 on the X axis
                currentOffset = Mathf.Round(currentOffset * 4f) / 4f;
                UpdateShader(currentOffset);
            }
        }

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
            // SWAPPED: Now applying the offset to X (the first value)
            reelMaterial.SetTextureOffset(texturePropertyName, new Vector2(offset, 0));
        }
    }
}