using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using UnityEngine.XR;

[DisallowMultipleComponent]
public class LanternGameManager : MonoBehaviour
{
    [Header("Rules")]
    public string returnSceneName = "Forest";
    public bool targetInitiallyUnlitLanternsOnly = true;
    public bool useAllLanternsIfNoneAreUnlit = true;
    public bool allowLanternsToTurnOff;
    public float lanternInteractDistance = 5f;

    [Header("HUD")]
    public float hudDistance = 2.35f;
    public float hudVerticalOffset = -0.55f;
    public float hudScale = 0.0032f;

    [Header("Completion")]
    public float exitPortalDistance = 4f;

    private readonly List<LanternController> allLanterns = new List<LanternController>();
    private readonly List<LanternController> targetLanterns = new List<LanternController>();

    private Canvas hudCanvas;
    private Text hudText;
    private GameObject exitPortal;
    private bool completed;

    private void Awake()
    {
        PauseMenu.ResetPauseState();
    }

    private void Start()
    {
        EnsureEventSystem();
        ConfigureLanterns();
        EnsureHud();
        EnsurePauseMenu();
        EnsureExitPortal();
        UpdateHud();
    }

    private void LateUpdate()
    {
        if (hudCanvas != null && hudCanvas.gameObject.activeSelf == PauseMenu.GameIsPaused)
        {
            hudCanvas.gameObject.SetActive(!PauseMenu.GameIsPaused);
        }

        PositionHud();
    }

    public void NotifyLanternChanged(LanternController lantern)
    {
        UpdateHud();

        if (!completed && targetLanterns.Count > 0 && CountLitTargets() >= targetLanterns.Count)
        {
            CompleteGame();
        }
    }

    private void ConfigureLanterns()
    {
        allLanterns.Clear();
        targetLanterns.Clear();

        LanternController[] lanterns = FindObjectsByType<LanternController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < lanterns.Length; i++)
        {
            LanternController lantern = lanterns[i];
            if (lantern == null)
            {
                continue;
            }

            allLanterns.Add(lantern);
            bool isTargetLantern = !targetInitiallyUnlitLanternsOnly || !lantern.isLit;
            if (isTargetLantern || lantern.pointLight != null || lantern.GetComponentInChildren<Light>(true) != null)
            {
                EnsurePointLight(lantern);
            }

            lantern.RefreshVisualState();
            EnsureLanternCollider(lantern);

            LanternInteractable interactable = lantern.GetComponent<LanternInteractable>();
            if (interactable == null)
            {
                interactable = lantern.gameObject.AddComponent<LanternInteractable>();
            }

            interactable.Configure(lantern, this, lanternInteractDistance, allowLanternsToTurnOff);

            if (isTargetLantern)
            {
                targetLanterns.Add(lantern);
            }
        }

        if (targetLanterns.Count == 0 && useAllLanternsIfNoneAreUnlit)
        {
            targetLanterns.AddRange(allLanterns);
            for (int i = 0; i < targetLanterns.Count; i++)
            {
                EnsurePointLight(targetLanterns[i]);
                targetLanterns[i].RefreshVisualState();
            }
        }
    }

    private void EnsurePointLight(LanternController lantern)
    {
        if (lantern.pointLight != null)
        {
            return;
        }

        Light existingLight = lantern.GetComponentInChildren<Light>(true);
        if (existingLight != null)
        {
            lantern.pointLight = existingLight.gameObject;
            return;
        }

        GameObject lightObject = new GameObject("Lantern Point Light");
        lightObject.transform.SetParent(lantern.transform, false);
        lightObject.transform.localPosition = new Vector3(0f, 0.18f, 0f);

        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.66f, 0.24f, 1f);
        light.range = 3f;
        light.intensity = 2.2f;

        lantern.pointLight = lightObject;
    }

    private void EnsureLanternCollider(LanternController lantern)
    {
        Collider[] colliders = lantern.GetComponentsInChildren<Collider>(true);
        if (colliders.Length > 0)
        {
            return;
        }

        SphereCollider collider = lantern.gameObject.AddComponent<SphereCollider>();
        collider.isTrigger = true;
        collider.radius = 0.45f;
        collider.center = Vector3.up * 0.2f;
    }

    private void EnsureHud()
    {
        if (hudCanvas != null)
        {
            return;
        }

        GameObject canvasObject = new GameObject("Lantern Game HUD");
        hudCanvas = canvasObject.AddComponent<Canvas>();
        hudCanvas.renderMode = RenderMode.WorldSpace;
        hudCanvas.sortingOrder = 50;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10f;

        RectTransform canvasRect = canvasObject.transform as RectTransform;
        canvasRect.sizeDelta = new Vector2(900f, 170f);

        Image background = CreateImage("HUD Background", canvasRect, new Color(0f, 0f, 0f, 0.55f));
        StretchToParent(background.rectTransform);

        hudText = CreateText("Objective Text", canvasRect, 40, TextAnchor.MiddleCenter);
        hudText.color = new Color(1f, 0.92f, 0.55f, 1f);
        hudText.resizeTextForBestFit = true;
        hudText.resizeTextMinSize = 20;
        hudText.resizeTextMaxSize = 44;
        StretchToParent(hudText.rectTransform);
        hudText.rectTransform.offsetMin = new Vector2(32f, 16f);
        hudText.rectTransform.offsetMax = new Vector2(-32f, -16f);

        PositionHud();
    }

    private void EnsurePauseMenu()
    {
        PauseMenu pauseMenu = FindFirstObjectByType<PauseMenu>(FindObjectsInactive.Include);
        if (pauseMenu == null)
        {
            pauseMenu = gameObject.AddComponent<PauseMenu>();
        }

        if (pauseMenu.pauseMenuUI == null)
        {
            pauseMenu.pauseMenuUI = CreatePauseMenuUI(pauseMenu);
        }

        pauseMenu.mainMenuSceneName = "MainMenu";
        pauseMenu.freezeGameWhenPaused = false;
        pauseMenu.pauseInputHand = XRNode.LeftHand;
        pauseMenu.menuInputHand = XRNode.RightHand;
        pauseMenu.followCameraWhileOpen = true;
        pauseMenu.menuDistance = 3f;
        pauseMenu.menuScale = 0.004f;
        pauseMenu.pauseMenuUI.SetActive(false);
    }

    private GameObject CreatePauseMenuUI(PauseMenu pauseMenu)
    {
        GameObject canvasObject = new GameObject("Pause Menu");
        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 80;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 10f;
        canvasObject.AddComponent<GraphicRaycaster>();

        RectTransform canvasRect = canvasObject.transform as RectTransform;
        canvasRect.sizeDelta = new Vector2(760f, 560f);

        Image panel = CreateImage("Panel", canvasRect, new Color(0.015f, 0.014f, 0.018f, 0.92f));
        StretchToParent(panel.rectTransform);

        Text title = CreateText("Title", canvasRect, 58, TextAnchor.MiddleCenter);
        title.text = "Paused";
        title.color = new Color(1f, 0.88f, 0.34f, 1f);
        title.rectTransform.anchorMin = new Vector2(0.5f, 1f);
        title.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        title.rectTransform.pivot = new Vector2(0.5f, 1f);
        title.rectTransform.anchoredPosition = new Vector2(0f, -48f);
        title.rectTransform.sizeDelta = new Vector2(680f, 100f);

        CreatePauseButton("ResumeButton", "Resume", canvasRect, 90f, pauseMenu.Resume);
        CreatePauseButton("MainMenuButton", "Main Menu", canvasRect, -35f, pauseMenu.LoadMenu);
        CreatePauseButton("QuitButton", "Quit", canvasRect, -160f, pauseMenu.QuitGame);

        return canvasObject;
    }

    private void CreatePauseButton(string objectName, string label, RectTransform parent, float y, UnityEngine.Events.UnityAction action)
    {
        GameObject buttonObject = new GameObject(objectName);
        buttonObject.transform.SetParent(parent, false);

        Image image = buttonObject.AddComponent<Image>();
        image.color = new Color(0.05f, 0.05f, 0.06f, 0.95f);

        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);

        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.05f, 0.05f, 0.06f, 0.95f);
        colors.highlightedColor = new Color(0.9f, 0.68f, 0.2f, 1f);
        colors.selectedColor = new Color(0.9f, 0.68f, 0.2f, 1f);
        colors.pressedColor = new Color(1f, 0.52f, 0.12f, 1f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        RectTransform buttonRect = buttonObject.transform as RectTransform;
        buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.pivot = new Vector2(0.5f, 0.5f);
        buttonRect.anchoredPosition = new Vector2(0f, y);
        buttonRect.sizeDelta = new Vector2(430f, 86f);

        Text text = CreateText("Text", buttonRect, 42, TextAnchor.MiddleCenter);
        text.text = label;
        text.color = Color.white;
        StretchToParent(text.rectTransform);
    }

    private void EnsureExitPortal()
    {
        if (exitPortal != null)
        {
            return;
        }

        exitPortal = new GameObject("Lantern Exit Portal");
        exitPortal.name = "Lantern Exit Portal";
        exitPortal.transform.position = GetExitPortalPosition();

        CapsuleCollider collider = exitPortal.AddComponent<CapsuleCollider>();
        collider.isTrigger = true;
        collider.radius = 1.4f;
        collider.height = 3.2f;
        collider.center = new Vector3(0f, 1.6f, 0f);

        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        visual.name = "Portal Ring";
        visual.transform.SetParent(exitPortal.transform, false);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = new Vector3(1.4f, 0.08f, 1.4f);

        Collider visualCollider = visual.GetComponent<Collider>();
        if (visualCollider != null)
        {
            Destroy(visualCollider);
        }

        Renderer renderer = visual.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material material = CreateRuntimeMaterial(new Color(0.1f, 0.8f, 1f, 0.75f));
            renderer.material = material;
        }

        GameObject lightObject = new GameObject("Portal Light");
        lightObject.transform.SetParent(exitPortal.transform, false);
        lightObject.transform.localPosition = Vector3.up * 1.3f;
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(0.35f, 0.9f, 1f, 1f);
        light.range = 4f;
        light.intensity = 2.5f;

        LanternExitPortal portal = exitPortal.AddComponent<LanternExitPortal>();
        portal.returnSceneName = returnSceneName;
        exitPortal.SetActive(false);
    }

    private Vector3 GetExitPortalPosition()
    {
        VRController player = FindFirstObjectByType<VRController>(FindObjectsInactive.Include);
        if (player != null)
        {
            Vector3 forward = player.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.forward;
            }

            return player.transform.position + forward.normalized * exitPortalDistance + Vector3.up * 0.05f;
        }

        return new Vector3(27f, 7.05f, 43f);
    }

    private void CompleteGame()
    {
        completed = true;
        if (exitPortal != null)
        {
            exitPortal.transform.position = GetExitPortalPosition();
            exitPortal.SetActive(true);
        }

        UpdateHud();
    }

    private int CountLitTargets()
    {
        int litCount = 0;
        for (int i = 0; i < targetLanterns.Count; i++)
        {
            if (targetLanterns[i] != null && targetLanterns[i].isLit)
            {
                litCount++;
            }
        }

        return litCount;
    }

    private void UpdateHud()
    {
        if (hudText == null)
        {
            return;
        }

        if (allLanterns.Count == 0)
        {
            hudText.text = "No lanterns found in this scene.";
            return;
        }

        if (completed)
        {
            hudText.text = "All lanterns are lit. Walk into the blue portal to return.";
            return;
        }

        int litCount = CountLitTargets();
        hudText.text = $"Light the dark lanterns: {litCount}/{targetLanterns.Count}";
    }

    private void PositionHud()
    {
        if (hudCanvas == null || Camera.main == null)
        {
            return;
        }

        Transform cameraTransform = Camera.main.transform;
        Transform canvasTransform = hudCanvas.transform;
        canvasTransform.position = cameraTransform.position +
                                   cameraTransform.forward * hudDistance +
                                   Vector3.up * hudVerticalOffset;
        canvasTransform.rotation = Quaternion.LookRotation(canvasTransform.position - cameraTransform.position, Vector3.up);
        canvasTransform.localScale = Vector3.one * hudScale;
    }

    private void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>(FindObjectsInactive.Include) != null)
        {
            return;
        }

        GameObject eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<InputSystemUIInputModule>();
    }

    private Image CreateImage(string objectName, Transform parent, Color color)
    {
        GameObject imageObject = new GameObject(objectName);
        imageObject.transform.SetParent(parent, false);
        Image image = imageObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private Text CreateText(string objectName, Transform parent, int fontSize, TextAnchor alignment)
    {
        GameObject textObject = new GameObject(objectName);
        textObject.transform.SetParent(parent, false);

        Text text = textObject.AddComponent<Text>();
        text.font = GetBuiltinFont();
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        return text;
    }

    private Font GetBuiltinFont()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null)
        {
            font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        return font;
    }

    private Material CreateRuntimeMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null || shader.name == "Hidden/InternalErrorShader")
        {
            shader = Shader.Find("Standard");
        }

        Material material = new Material(shader);
        material.color = color;
        material.EnableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", color * 1.8f);
        return material;
    }

    private void StretchToParent(RectTransform rectTransform)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = Vector2.zero;
    }
}
