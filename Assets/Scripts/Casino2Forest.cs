using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneTransitionDoor : MonoBehaviour
{
    [SerializeField] private string sceneToLoad = "Forest";
    [SerializeField] private AudioClip doorSFX;
    [SerializeField] private float sfxDelay = 0.8f;

    private AudioSource audioSource;
    private bool isTransitioning;

    private void Awake()
    {
        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f;

        if (doorSFX == null)
            doorSFX = GenerateDoorSFX();
    }

    private void OnTriggerEnter(Collider other)
    {
        Debug.Log($"<color=yellow>DOOR TOUCHED BY:</color> {other.gameObject.name} | <color=orange>TAG:</color> {other.tag}");

        if (other.CompareTag("Player") && !isTransitioning)
        {
            Debug.Log("<color=green>Player detected! Loading scene...</color>");
            isTransitioning = true;
            audioSource.PlayOneShot(doorSFX);
            StartCoroutine(LoadAfterSFX());
        }
        else
        {
            Debug.Log("<color=red>Touch detected, but it wasn't the Player tag.</color>");
        }
    }

    private IEnumerator LoadAfterSFX()
    {
        yield return new WaitForSeconds(sfxDelay);
        SceneManager.LoadScene(sceneToLoad);
    }

    private AudioClip GenerateDoorSFX()
    {
        int sampleRate = 44100;
        int samples = (int)(sampleRate * 0.7f);
        float[] data = new float[samples];

        for (int i = 0; i < samples; i++)
        {
            float t = (float)i / sampleRate;
            float env = Mathf.Clamp01(1f - t / 0.7f);
            env *= env;

            // Heavy wooden thud
            float thud = Mathf.Sin(2f * Mathf.PI * 80f * t) * Mathf.Exp(-t * 18f);
            // Door creak sweep
            float creak = Mathf.Sin(2f * Mathf.PI * (300f + 200f * t) * t) * Mathf.Exp(-t * 5f) * 0.3f;
            // Latch click
            float click = (t > 0.05f && t < 0.08f) ? Mathf.Sin(2f * Mathf.PI * 2500f * t) * 0.4f * Mathf.Exp(-(t - 0.05f) * 120f) : 0f;

            data[i] = (thud + creak + click) * env;
        }

        AudioClip clip = AudioClip.Create("DoorSFX", samples, 1, sampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}