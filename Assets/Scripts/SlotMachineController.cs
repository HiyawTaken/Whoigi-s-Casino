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
    public int starterSpinCount = 10;
    // Money payout amounts for: [0] Mushroom, [1] Fire Flower, [2] Star, [3] Leaf
    public int[] payouts = { 10, 25, 100, 50 };

    [Header("Celebration")]
    public Transform celebrationOrigin;
    public int celebrationBurstCount = 95;
    public float celebrationDuration = 2.8f;
    public float celebrationHeight = 2.5f;
    public Color celebrationColor = new Color(1f, 0.85f, 0.18f, 1f);
    public AudioClip celebrationSFX;

    [Header("Events")]
    public UnityEvent onSpinStart;
    public UnityEvent onJackpot;
    public UnityEvent onNoFunds;

    private bool isSpinning = false;
    private bool leverReady = true;
    private string textureProp = "_BaseMap";
    private AudioSource celebrationAudioSource;
    private Material celebrationParticleMaterial;
    private Coroutine celebrationCoroutine;

    public int SpinCost => Mathf.Max(0, spinCost);
    public bool IsSpinning => isSpinning;

    private void Awake()
    {
        ApplyEconomyDefaults();
    }

    private void OnValidate()
    {
        ApplyEconomyDefaults();
    }

    void Start()
    {
        ApplyEconomyDefaults();
        PlayerData wallet = PlayerData.EnsureExists();
        if (wallet != null)
        {
            wallet.EnsureStarterTokens(SpinCost * starterSpinCount);
        }

        // Detect if using URP (_BaseMap) or Standard (_MainTex)
        if (reelRenderers.Length > 0 && reelRenderers[0] != null && !reelRenderers[0].material.HasProperty("_BaseMap"))
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
        PlayerData wallet = PlayerData.EnsureExists();
        int cost = SpinCost;

        if (wallet != null && wallet.TrySpendTokens(cost))
        {
            StartCoroutine(SpinReels());
            return;
        }

        Debug.Log($"Not enough tokens to spin. Need {cost}, have {PlayerData.tokens}.");
        onNoFunds?.Invoke();
    }

    public bool CanAffordSpin()
    {
        PlayerData wallet = PlayerData.EnsureExists();
        return wallet != null && wallet.CanAffordTokens(SpinCost);
    }

    public string GetLeverPrompt()
    {
        string tokenWord = SpinCost == 1 ? "token" : "tokens";
        if (isSpinning)
        {
            return "Spinning...";
        }

        return CanAffordSpin()
            ? $"Press Grip to spin ({SpinCost} {tokenWord})"
            : $"Need {SpinCost} {tokenWord} to spin";
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
        onSpinStart?.Invoke();

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
            int wonAmount = GetPayout(winningIconIndex);

            Debug.Log($"JACKPOT! Landed on icon {winningIconIndex}. You won ${wonAmount}!");

            PlayerData wallet = PlayerData.EnsureExists();
            if (wallet != null)
            {
                wallet.AddMoney(wonAmount);
            }

            PlayWinCelebration(wonAmount);
            onJackpot?.Invoke();
        }
    }

    private void PlayWinCelebration(int wonAmount)
    {
        if (celebrationCoroutine != null)
        {
            StopCoroutine(celebrationCoroutine);
        }

        celebrationCoroutine = StartCoroutine(WinCelebrationRoutine(wonAmount));
    }

    private IEnumerator WinCelebrationRoutine(int wonAmount)
    {
        Vector3 origin = GetCelebrationPosition();
        float visualScale = GetCelebrationVisualScale();
        ParticleSystem particles = CreateCelebrationParticles(origin);
        TextMesh winText = CreateWinText(origin, wonAmount);

        if (particles != null)
        {
            particles.Emit(Mathf.Max(20, celebrationBurstCount));
        }

        PlayCelebrationSFX();

        float elapsed = 0f;
        Vector3 textStart = winText != null ? winText.transform.position : origin;
        while (elapsed < celebrationDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, celebrationDuration));

            if (winText != null)
            {
                winText.transform.position = textStart + Vector3.up * (t * 1.2f * visualScale);
                winText.transform.localScale = Vector3.one * Mathf.Lerp(1.25f, 1f, t);
                FaceCamera(winText.transform);

                Color color = celebrationColor;
                color.a = 1f - Mathf.SmoothStep(0.65f, 1f, t);
                winText.color = color;
            }

            yield return null;
        }

        if (particles != null)
        {
            Destroy(particles.gameObject, 1.5f);
        }

        if (winText != null)
        {
            Destroy(winText.gameObject);
        }

        celebrationCoroutine = null;
    }

    private Vector3 GetCelebrationPosition()
    {
        if (celebrationOrigin != null)
        {
            return celebrationOrigin.position;
        }

        Bounds bounds = default;
        bool hasBounds = false;
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        if (hasBounds)
        {
            return bounds.center + Vector3.up * Mathf.Max(1f, bounds.extents.y * 0.8f);
        }

        return transform.position + Vector3.up * celebrationHeight * GetCelebrationVisualScale();
    }

    private ParticleSystem CreateCelebrationParticles(Vector3 origin)
    {
        float visualScale = GetCelebrationVisualScale();
        GameObject particleObject = new GameObject("Slot Win Celebration");
        particleObject.transform.position = origin + Vector3.up * 0.5f * visualScale;

        ParticleSystem particles = particleObject.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = particles.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = 0.75f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.1f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3.5f * visualScale, 7.5f * visualScale);
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f * visualScale, 0.28f * visualScale);
        main.startColor = new ParticleSystem.MinMaxGradient(
            celebrationColor,
            new Color(1f, 0.38f, 0.08f, 1f));
        main.gravityModifier = 0.55f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = false;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 32f;
        shape.radius = 0.35f * visualScale;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = particles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(celebrationColor, 0f),
                new GradientColorKey(new Color(1f, 0.45f, 0.1f, 1f), 0.55f),
                new GradientColorKey(Color.white, 1f),
            },
            new[]
            {
                new GradientAlphaKey(1f, 0f),
                new GradientAlphaKey(1f, 0.65f),
                new GradientAlphaKey(0f, 1f),
            });
        colorOverLifetime.color = gradient;

        ParticleSystemRenderer particleRenderer = particles.GetComponent<ParticleSystemRenderer>();
        if (particleRenderer != null)
        {
            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            particleRenderer.material = GetCelebrationParticleMaterial();
        }

        return particles;
    }

    private TextMesh CreateWinText(Vector3 origin, int wonAmount)
    {
        float visualScale = GetCelebrationVisualScale();
        GameObject textObject = new GameObject("Slot Win Text");
        textObject.transform.position = origin + Vector3.up * celebrationHeight * visualScale;

        TextMesh text = textObject.AddComponent<TextMesh>();
        text.text = $"JACKPOT!\n+${wonAmount}";
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.fontSize = 96;
        text.characterSize = 0.14f * visualScale;
        text.color = celebrationColor;

        MeshRenderer renderer = textObject.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.sortingOrder = 100;
        }

        FaceCamera(textObject.transform);
        return text;
    }

    private float GetCelebrationVisualScale()
    {
        Vector3 scale = transform.lossyScale;
        float maxScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
        return Mathf.Clamp(maxScale, 1f, 8f);
    }

    private void FaceCamera(Transform target)
    {
        Camera camera = Camera.main;
        if (target == null || camera == null)
        {
            return;
        }

        Vector3 lookDirection = target.position - camera.transform.position;
        if (lookDirection.sqrMagnitude > 0.001f)
        {
            target.rotation = Quaternion.LookRotation(lookDirection, Vector3.up);
        }
    }

    private void PlayCelebrationSFX()
    {
        if (celebrationAudioSource == null)
        {
            celebrationAudioSource = gameObject.AddComponent<AudioSource>();
            celebrationAudioSource.playOnAwake = false;
            celebrationAudioSource.spatialBlend = 1f;
            celebrationAudioSource.volume = 0.85f;
        }

        if (celebrationSFX == null)
        {
            celebrationSFX = GenerateCelebrationSFX();
        }

        celebrationAudioSource.PlayOneShot(celebrationSFX);
    }

    private Material GetCelebrationParticleMaterial()
    {
        if (celebrationParticleMaterial != null)
        {
            return celebrationParticleMaterial;
        }

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null || shader.name == "Hidden/InternalErrorShader")
        {
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }

        if (shader == null || shader.name == "Hidden/InternalErrorShader")
        {
            shader = Shader.Find("Standard");
        }

        celebrationParticleMaterial = new Material(shader);
        celebrationParticleMaterial.color = Color.white;
        return celebrationParticleMaterial;
    }

    private AudioClip GenerateCelebrationSFX()
    {
        const int sampleRate = 44100;
        float duration = 1.25f;
        int samples = Mathf.CeilToInt(sampleRate * duration);
        float[] data = new float[samples];
        float[] notes = { 523.25f, 659.25f, 783.99f, 1046.5f };

        for (int i = 0; i < samples; i++)
        {
            float t = i / (float)sampleRate;
            float value = 0f;

            for (int n = 0; n < notes.Length; n++)
            {
                float noteStart = n * 0.18f;
                if (t < noteStart)
                {
                    continue;
                }

                float localT = t - noteStart;
                float envelope = Mathf.Exp(-localT * 4.8f) * Mathf.Clamp01(localT * 22f);
                value += Mathf.Sin(2f * Mathf.PI * notes[n] * t) * envelope;
            }

            float sparkle = Mathf.Sin(2f * Mathf.PI * 1800f * t) * Mathf.Exp(-t * 5.5f) * 0.12f;
            data[i] = Mathf.Clamp((value * 0.18f) + sparkle, -1f, 1f);
        }

        AudioClip clip = AudioClip.Create("SlotWinCelebrationSFX", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private int GetPayout(int iconIndex)
    {
        if (payouts == null || iconIndex < 0 || iconIndex >= payouts.Length)
        {
            return 0;
        }

        return Mathf.Max(0, payouts[iconIndex]);
    }

    private void ApplyEconomyDefaults()
    {
        if (spinCost < 0)
        {
            spinCost = 0;
        }

        if (starterSpinCount <= 0)
        {
            starterSpinCount = 10;
        }
    }
}
