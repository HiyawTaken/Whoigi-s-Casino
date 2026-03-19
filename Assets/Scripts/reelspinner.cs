using UnityEngine;
using UnityEngine.XR;
using System.Collections.Generic;

public class ReelSpinner : MonoBehaviour
{
    [Header("Settings")]
    public float spinSpeed = 8f;

    private bool isSpinning = false;
    private float currentOffset = 0f;
    private Material reelMaterial;
    private string texturePropertyName = "_BaseMap";

    private InputDevice rightController;
    private bool wasPressed = false;

    void Start()
    {
        Renderer rend = GetComponent<Renderer>();
        if (rend != null)
        {
            reelMaterial = rend.material;
            if (!reelMaterial.HasProperty("_BaseMap") && reelMaterial.HasProperty("_MainTex"))
            {
                texturePropertyName = "_MainTex";
            }
        }
    }

    void Update()
    {
        if (!rightController.isValid)
        {
            var devices = new List<InputDevice>();
            InputDevices.GetDevicesWithCharacteristics(
                InputDeviceCharacteristics.Right | InputDeviceCharacteristics.Controller, devices);
            if (devices.Count > 0) rightController = devices[0];
        }

        rightController.TryGetFeatureValue(CommonUsages.secondaryButton, out bool bButton); // B button

        bool pressed = Input.GetKeyDown(KeyCode.Space) || bButton;

        if (pressed && !wasPressed)
        {
            isSpinning = !isSpinning;
            if (!isSpinning)
            {
                currentOffset = Mathf.Round(currentOffset * 4f) / 4f;
                UpdateShader(currentOffset);
            }
        }

        wasPressed = bButton;

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
            reelMaterial.SetTextureOffset(texturePropertyName, new Vector2(offset, 0));
        }
    }
}