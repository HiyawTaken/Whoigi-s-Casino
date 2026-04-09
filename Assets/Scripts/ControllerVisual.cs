using UnityEngine;
using UnityEngine.XR;

public class ControllerVisual : MonoBehaviour
{
    [Header("Settings")]
    public XRNode hand = XRNode.RightHand;
    public Color handColor = new Color(0.2f, 0.6f, 1f);
    public float sphereSize = 0.05f;

    private GameObject handSphere;
    private GameObject pointer;
    private InputDevice device;

    void Start()
    {
        handSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        handSphere.name = "HandVisual";
        handSphere.transform.SetParent(transform.root);
        handSphere.transform.localScale = Vector3.one * sphereSize;
        Destroy(handSphere.GetComponent<Collider>());

        pointer = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pointer.name = "Pointer";
        pointer.transform.SetParent(handSphere.transform);
        pointer.transform.localPosition = new Vector3(0f, 0f, 1.2f);
        pointer.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        pointer.transform.localScale = new Vector3(0.2f, 1f, 0.2f);
        Destroy(pointer.GetComponent<Collider>());

        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        if (mat.shader.name == "Hidden/InternalErrorShader")
            mat = new Material(Shader.Find("Standard"));
        mat.color = handColor;
        handSphere.GetComponent<Renderer>().material = mat;
        pointer.GetComponent<Renderer>().material = mat;
    }

    void Update()
    {
        if (!device.isValid)
            device = InputDevices.GetDeviceAtXRNode(hand);

        if (device.isValid)
        {
            if (device.TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 pos))
                handSphere.transform.localPosition = pos;

            if (device.TryGetFeatureValue(CommonUsages.deviceRotation, out Quaternion rot))
                handSphere.transform.localRotation = rot;
        }
    }
}
