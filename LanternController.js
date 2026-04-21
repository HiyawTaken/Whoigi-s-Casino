public class LanternController : MonoBehaviour 
{
    public bool isLit;
    public GameObject pointLight; // Reference to the Point Light component
    public Material litMaterial;   // Material with emission
    public Material unlitMaterial; // Standard material

    public void ToggleLantern() 
    {
        isLit = !isLit;
        pointLight.SetActive(isLit);
        GetComponent<Renderer>().material = isLit ? litMaterial : unlitMaterial;
    }
}
