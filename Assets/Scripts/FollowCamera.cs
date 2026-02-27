using UnityEngine;

public class FollowCamera : MonoBehaviour
{
    public Transform cameraTransform;
    public float distance = 2f;
    public float verticalOffset = 0f;

    void LateUpdate()
    {
        transform.position = cameraTransform.position + cameraTransform.forward * distance + Vector3.up * verticalOffset;
        transform.rotation = cameraTransform.rotation;
    }
}