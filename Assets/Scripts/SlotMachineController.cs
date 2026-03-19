using UnityEngine;
using System.Collections;
using UnityEngine.Events;

public class SlotMachineController : MonoBehaviour
{
    [Header("Reel Settings")]
    public Renderer[] reelRenderers;
    public float spinDuration = 3f;

    [Header("Lever Settings")]
    public Transform leverHandle;
    public float pullThreshold = 55f;

    [Header("Economy")]
    public int spinCost = 1; // Costs 1 Token to spin
    // Payout amounts for: [0] Mushroom, [1] Fire Flower, [2] Star, [3] Leaf
    public int[] payouts = { 10, 25, 100, 50 };

    [Header("Events")]
    public UnityEvent onSpinStart;
    public UnityEvent onJackpot;

    private bool isSpinning = false;
    private bool leverReady = true;
    private string textureProp = "_BaseMap";

    void Start()
    {
        if (reelRenderers.Length > 0 && !reelRenderers[0].material.HasProperty("_BaseMap"))
        {
            textureProp = "_MainTex";
        }
    }

    void Update()
    {
        float angle = leverHandle.localEulerAngles.x;
        if (angle > 180) angle -= 360;

        if (angle > pullThreshold && leverReady && !isSpinning)
        {
            // FIX: Changed PlayerData.Instance.CurrentTokens to PlayerData.tokens
            // Because 'tokens' is static, we can read it directly from the class name!
            if (PlayerData.Instance != null && PlayerData.tokens >= spinCost)
            {
                // Deduct the token (AddTokens is still an instance method, so this stays the same)
                PlayerData.Instance.AddTokens(-spinCost);

                StartCoroutine(SpinReels());
                leverReady = false;
            }
            else
            {
                Debug.Log("Not enough tokens to spin!");
                // You could trigger an "Error" sound here later
            }
        }

        if (angle < 5f) leverReady = true;
    }

    IEnumerator SpinReels()
    {
        isSpinning = true;
        onSpinStart.Invoke();

        int[] results = { Random.Range(0, 4), Random.Range(0, 4), Random.Range(0, 4) };
        float[] currentOffsets = new float[reelRenderers.Length];

        float elapsed = 0;
        float speed = 10f;

        while (elapsed < spinDuration)
        {
            elapsed += Time.deltaTime;
            for (int i = 0; i < reelRenderers.Length; i++)
            {
                currentOffsets[i] += Time.deltaTime * speed;
                reelRenderers[i].material.SetTextureOffset(textureProp, new Vector2(currentOffsets[i], 0));
            }
            yield return null;
        }

        for (int i = 0; i < reelRenderers.Length; i++)
        {
            float finalOffset = results[i] * 0.25f;
            reelRenderers[i].material.SetTextureOffset(textureProp, new Vector2(finalOffset, 0));
            yield return new WaitForSeconds(0.3f);
        }

        // Check Win and Award Money
        if (results[0] == results[1] && results[1] == results[2])
        {
            int winningIconIndex = results[0];
            int wonAmount = payouts[winningIconIndex];

            Debug.Log($"JACKPOT! Landed on icon {winningIconIndex}. You won ${wonAmount}!");

            if