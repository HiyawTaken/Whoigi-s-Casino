using UnityEngine;
using UnityEngine.InputSystem;
using XRCommonUsages = UnityEngine.XR.CommonUsages;
using XRInputDevice = UnityEngine.XR.InputDevice;
using XRInputDevices = UnityEngine.XR.InputDevices;
using XRNode = UnityEngine.XR.XRNode;

[DisallowMultipleComponent]
public class LanternInteractable : MonoBehaviour
{
    [Header("References")]
    public LanternController lantern;
    public LanternGameManager gameManager;

    [Header("Interaction")]
    public float interactDistance = 5f;
    public bool allowTurningOff;
    public string lightPrompt = "Press Grip to light";
    public string togglePrompt = "Press Grip to toggle";

    [Header("Prompt")]
    public Color promptColor = new Color(1f, 0.88f, 0.34f, 1f);
    public Color promptShadowColor = new Color(0f, 0f, 0f, 0.85f);
    public float promptCharacterSize = 0.045f;
    public float promptHeight = 0.18f;

    private ControllerVisual[] controllerHands;
    private Transform promptRoot;
    private TextMesh promptText;
    private TextMesh promptShadow;
    private bool previousInteractPressed;
    private float nextHandRefreshTime;

    private void Awake()
    {
        if (lantern == null)
        {
            lantern = GetComponent<LanternController>();
        }
    }

    private void OnDisable()
    {
        SetPromptVisible(false);
        previousInteractPressed = false;
    }

    private void OnDestroy()
    {
        if (promptRoot != null)
        {
            Destroy(promptRoot.gameObject);
        }
    }

    private void Update()
    {
        if (PauseMenu.GameIsPaused || lantern == null)
        {
            SetPromptVisible(false);
            previousInteractPressed = false;
            return;
        }

        bool canInteract = allowTurningOff || !lantern.isLit;
        float distance = GetClosestControllerDistance();
        bool isClose = distance <= interactDistance;

        if (canInteract && isClose)
        {
            ShowPrompt();
        }
        else
        {
            SetPromptVisible(false);
        }

        bool interactPressed = ReadInteractPressed();
        if (canInteract && isClose && interactPressed && !previousInteractPressed)
        {
            lantern.ToggleLantern();
            gameManager?.NotifyLanternChanged(lantern);
            SetPromptVisible(false);
        }

        previousInteractPressed = interactPressed;
    }

    public void Configure(LanternController targetLantern, LanternGameManager manager, float distance, bool canTurnOff)
    {
        lantern = targetLantern;
        gameManager = manager;
        interactDistance = distance;
        allowTurningOff = canTurnOff;
    }

    private float GetClosestControllerDistance()
    {
        RefreshHandsIfNeeded();

        Vector3 targetPosition = GetFocusPosition();
        float closestDistance = float.PositiveInfinity;

        if (controllerHands != null)
        {
            for (int i = 0; i < controllerHands.Length; i++)
            {
                ControllerVisual hand = controllerHands[i];
                if (hand == null || !hand.isActiveAndEnabled)
                {
                    continue;
                }

                float distance = Vector3.Distance(hand.transform.position, targetPosition);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                }
            }
        }

        if (!float.IsPositiveInfinity(closestDistance))
        {
            return closestDistance;
        }

        Camera camera = Camera.main;
        return camera != null ? Vector3.Distance(camera.transform.position, targetPosition) : float.PositiveInfinity;
    }

    private void RefreshHandsIfNeeded()
    {
        if (controllerHands != null && Time.unscaledTime < nextHandRefreshTime)
        {
            return;
        }

        controllerHands = FindObjectsByType<ControllerVisual>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        nextHandRefreshTime = Time.unscaledTime + 1f;
    }

    private bool ReadInteractPressed()
    {
        bool controllerPressed = ReadDevicePressed(XRNode.LeftHand) || ReadDevicePressed(XRNode.RightHand);
        bool keyboardPressed = Keyboard.current != null && Keyboard.current.eKey.isPressed;
        return controllerPressed || keyboardPressed;
    }

    private bool ReadDevicePressed(XRNode node)
    {
        XRInputDevice device = XRInputDevices.GetDeviceAtXRNode(node);
        if (!device.isValid)
        {
            return false;
        }

        if (device.TryGetFeatureValue(XRCommonUsages.grip, out float grip) && grip >= 0.55f)
        {
            return true;
        }

        if (device.TryGetFeatureValue(XRCommonUsages.gripButton, out bool gripButton) && gripButton)
        {
            return true;
        }

        if (device.TryGetFeatureValue(XRCommonUsages.triggerButton, out bool triggerButton) && triggerButton)
        {
            return true;
        }

        return device.TryGetFeatureValue(XRCommonUsages.primaryButton, out bool primaryButton) && primaryButton;
    }

    private void ShowPrompt()
    {
        EnsurePrompt();

        string prompt = allowTurningOff ? togglePrompt : lightPrompt;
        promptRoot.position = GetPromptPosition();
        FacePromptTowardCamera();
        promptText.text = prompt;
        promptShadow.text = prompt;
        promptText.color = promptColor;
        promptShadow.color = promptShadowColor;
        promptText.characterSize = promptCharacterSize;
        promptShadow.characterSize = promptCharacterSize;
        SetPromptVisible(true);
    }

    private void EnsurePrompt()
    {
        if (promptRoot != null)
        {
            return;
        }

        GameObject root = new GameObject("Lantern Prompt");
        root.hideFlags = HideFlags.DontSave;
        promptRoot = root.transform;
        promptText = CreatePromptText(root.transform, "Prompt Text", Vector3.zero, promptColor);
        promptShadow = CreatePromptText(root.transform, "Prompt Shadow", new Vector3(0.008f, -0.008f, 0.002f), promptShadowColor);
    }

    private TextMesh CreatePromptText(Transform parent, string objectName, Vector3 localOffset, Color color)
    {
        GameObject textObject = new GameObject(objectName);
        textObject.hideFlags = HideFlags.DontSave;
        textObject.transform.SetParent(parent, false);
        textObject.transform.localPosition = localOffset;
        textObject.transform.localRotation = Quaternion.identity;

        TextMesh textMesh = textObject.AddComponent<TextMesh>();
        textMesh.text = lightPrompt;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.characterSize = promptCharacterSize;
        textMesh.fontSize = 64;
        textMesh.color = color;

        MeshRenderer renderer = textObject.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.sortingOrder = 100;
        }

        return textMesh;
    }

    private void SetPromptVisible(bool visible)
    {
        if (promptRoot != null && promptRoot.gameObject.activeSelf != visible)
        {
            promptRoot.gameObject.SetActive(visible);
        }
    }

    private Vector3 GetFocusPosition()
    {
        if (TryGetBounds(out Bounds bounds))
        {
            return bounds.center;
        }

        return transform.position;
    }

    private Vector3 GetPromptPosition()
    {
        if (TryGetBounds(out Bounds bounds))
        {
            return bounds.center + Vector3.up * (bounds.extents.y + promptHeight);
        }

        return transform.position + Vector3.up * promptHeight;
    }

    private bool TryGetBounds(out Bounds bounds)
    {
        bounds = default;
        bool hasBounds = false;

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            if (!hasBounds)
            {
                bounds = renderers[i].bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
        }

        return hasBounds;
    }

    private void FacePromptTowardCamera()
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            return;
        }

        Vector3 toPrompt = promptRoot.position - camera.transform.position;
        if (toPrompt.sqrMagnitude > 0.0001f)
        {
            promptRoot.rotation = Quaternion.LookRotation(toPrompt.normalized, Vector3.up);
        }
    }
}
