using UnityEngine;

public class ReelTest : MonoBehaviour
{
    public Renderer reelRenderer;
    public float spinSpeed = 2.0f;
    private bool isSpinning = false;
    private float currentOffset = 0f;

    void Update()
    {
        // Press the Space bar to start/stop the spin
        if (Input.GetKeyDown(KeyCode.Space))
        {
            isSpinning = !isSpinning;
        }

        if (isSpinning)
        {
            // Calculate the new offset based on time
            currentOffset += Time.deltaTime * spinSpeed;
            
            // Apply the offset to the Material
            // "_BaseMap" is for URP shaders. Use "_MainTex" if using standard.
            reelRenderer.material.SetTextureOffset("_BaseMap", new Vector2(0, currentOffset));
        }
    }
}