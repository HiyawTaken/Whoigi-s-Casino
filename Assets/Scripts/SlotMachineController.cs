using UnityEngine;
using System.Collections;
using UnityEngine.Events;

public class SlotMachineController : MonoBehaviour
{
    [Header("Reel Settings")]
    public Transform[] reels;
    public float spinDuration = 3f;
    
    [Header("Lever Settings")]
    public Transform leverHandle;
    public float pullThreshold = 55f; // Trigger when lever hits 55 degrees
    
    [Header("Events")]
    public UnityEvent onSpinStart;
    public UnityEvent onJackpot;

    private bool isSpinning = false;
    private bool leverReady = true;

    void Update()
    {
        // Check lever rotation to trigger spin
        float angle = leverHandle.localEulerAngles.x; 
        // Note: Depending on your model, you might need to normalize this angle
        if (angle > pullThreshold && leverReady && !isSpinning)
        {
            StartCoroutine(SpinReels());
            leverReady = false; // Prevent double-triggering
        }
        
        if (angle < 5f) leverReady = true; // Reset when lever is back up
    }

    IEnumerator SpinReels()
    {
        isSpinning = true;
        onSpinStart.Invoke();

        // 1. Determine Results (0-9 for 10 symbols)
        int[] results = { Random.Range(0, 10), Random.Range(0, 10), Random.Range(0, 10) };

        float elapsed = 0;
        while (elapsed < spinDuration)
        {
            for (int i = 0; i < reels.Length; i++)
            {
                // Spin faster at start, slower at end
                float speed = Mathf.Lerp(1500f, 200f, elapsed / spinDuration);
                reels[i].Rotate(Vector3.up * speed * Time.deltaTime);
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        // 2. Snap to final positions
        for (int i = 0; i < reels.Length; i++)
        {
            float finalAngle = results[i] * 36f; // 360 / 10 symbols = 36 degrees
            reels[i].localEulerAngles = new Vector3(0, finalAngle, 0);
            // Add a "thud" sound or haptic here
            yield return new WaitForSeconds(0.3f); 
        }

        // 3. Check for Win
        if (results[0] == results[1] && results[1] == results[2])
        {
            onJackpot.Invoke();
        }

        isSpinning = false;
    }
}