using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

public class SceneFader : MonoBehaviour
{
    public Image fadeImage;   // The black panel image
    public float fadeSpeed = 1f;

    private void Awake()
    {
        fadeImage.color = new Color(0, 0, 0, 0); // transparent
    }

    public void FadeToScene(string sceneName)
    {
        StartCoroutine(FadeOutIn(sceneName));
    }

    IEnumerator FadeOutIn(string sceneName)
    {
        // Fade to black
        float t = 0;
        while (t < 1)
        {
            t += Time.deltaTime * fadeSpeed;
            fadeImage.color = new Color(0, 0, 0, t);
            yield return null;
        }

        // Load scene
        SceneManager.LoadScene(sceneName);

        // Wait one frame for the new scene to load
        yield return null;

        // Fade back in
        t = 1;
        while (t > 0)
        {
            t -= Time.deltaTime * fadeSpeed;
            fadeImage.color = new Color(0, 0, 0, t);
            yield return null;
        }
    }
}