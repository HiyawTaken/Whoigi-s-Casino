using UnityEngine;
using TMPro;

public class ProximityLabel : MonoBehaviour
{
    [Header("References")]
    public Transform capsule;          // Drag your Capsule here
    public TextMeshProUGUI label;      // Drag your TextMeshPro here

    [Header("Settings")]
    public float activationRadius = 3f;

    private Transform _playerHead;

    void Start()
    {
        // Finds the main camera — works for Quest, SteamVR, XR Toolkit
        _playerHead = Camera.main.transform;
        label.enabled = false;
    }

    void Update()
    {
        float dist = Vector3.Distance(_playerHead.position, capsule.position);
        label.enabled = dist <= activationRadius;
    }
}