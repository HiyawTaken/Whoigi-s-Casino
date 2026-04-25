using UnityEngine;
using System.Collections;
using UnityEngine.Events;

public class SlotMachineController : MonoBehaviour
{
    [Header("Reel Settings")]
    public Renderer[] reelRenderers;
    public float spinDuration = 3f;
    public int iconCount = 4; // Mushroom, Fire Flower, Star, Leaf
    public float spinSpeed = 10f;

    [Header("Lever Settings")]
    public Transform leverHandle;
    public float pullThreshold = 55f;

    [Header("Economy")]
    public int spinCost = 1;
    // Payout amounts for: [0] Mushroom, [1] Fire Flower, [2] Star, [3] Leaf
    public int[] payouts = { 10, 25, 100, 50 };

    [Header("Events")]
    public UnityEvent onSpinStart;
    public UnityEvent onJackpot;
    public UnityEvent onNoFunds;

    private bool isSpinning = false;
    private bool leverReady = true;
    private string textureProp = "_BaseMap";

    void Start()
    {
        // Detect if using URP (_BaseMap) or Standard (_MainTex)
        if (reelRenderers.Length > 0 && !reelRenderers[0].material.HasProperty("_BaseMap"))
        {
            textureProp = "_MainTex";
        }
    }

    void Update()
    {
        // CHANGED: Reading the Z axis instead of X
        float angle = leverHandle.localEulerAngles.z;

        // Normalize angle to -180 to 180 range
        if (angle > 180) angle -= 360;

        // Use Mathf.Abs so it works regardless of pull direction (+ or -)
        if (Mathf.Abs(angle) > pullThreshold && leverReady && !isSpinning)
        {
            AttemptSpin();
            leverReady = false;
        }

        // Reset readiness when the lever is back near the top (0 degrees)
        if (Mathf.Abs(angle) < 5f)
        {
            leverReady = true;
        }
    }

    void AttemptSpin()
    {
        // Check PlayerData for tokens
        if (PlayerData.Instance != null && PlayerData.tokens >= spinCost)
        {
            PlayerData.Instance.AddTokens(-spinCost);
            StartCoroutine(SpinReels());
        }
        else
        {
            Debug.Log("Not enough tokens to spin!");
            onNoFunds.Invoke();
        }
    }

    public void PullLever()
    {
        if (!leverReady || isSpinning)
            return;

        AttemptSpin();
        leverReady = false;
    }

    IEnumerator SpinReels()
    {
        isSpinning = true;
        onSpinStart.Invoke();

        // Determine results (indices 0-3)
        int[] results = { Random.Range(0, iconCount), Random.Range(0, iconCount), Random.Range(0, iconCount) };
        float[] currentOffsets = new float[reelRenderers.Length];

        // Initialize offsets from current material position to prevent "jumping"
        for (int i = 0; i < reelRenderers.Length; i++)
        {
            currentOffsets[i] = reelRenderers[i].material.GetTextureOffset(textureProp).x;
        }

        float elapsed = 0;

        // Fast Spinning Phase
        while (elapsed < spinDuration)
        {
            elapsed += Time.deltaTime;
            for (int i = 0; i < reelRenderers.Length; i++)
            {
                currentOffsets[i] += Time.deltaTime * spinSpeed;
                float wrappedOffset = currentOffsets[i] % 1.0f; // Keep offset between 0-1
                reelRenderers[i].material.SetTextureOffset(textureProp, new Vector2(wrappedOffset, 0));
            }
            yield return null;
        }

        // Individual Stopping Phase
        float stepSize = 1f / iconCount;
        for (int i = 0; i < reelRenderers.Length; i++)
        {
            float finalOffset = results[i] * stepSize;
            reelRenderers[i].material.SetTextureOffset(textureProp, new Vector2(finalOffset, 0));
            yield return new WaitForSeconds(0.4f); // Delay between each reel stop
        }

        CheckResults(results);
        isSpinning = false;
    }

    void CheckResults(int[] results)
    {
        // Check for 3-of-a-kind
        if (results[0] == results[1] && results[1] == results[2])
        {
            int winningIconIndex = results[0];
            int wonAmount = payouts[winningIconIndex];

            Debug.Log($"JACKPOT! Landed on icon {winningIconIndex}. You won ${wonAmount}!");

            if (PlayerData.Instance != null)
            {
                PlayerData.Instance.AddMoney(wonAmount);
            }

            onJackpot.Invoke();
        }
    }
}
