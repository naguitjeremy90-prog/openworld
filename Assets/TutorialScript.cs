using UnityEngine;
using System.Collections;

public class TutorialStartDelay : MonoBehaviour
{
    public CanvasGroup moveTextGroup;
    public float delay = 5f;
    public float fadeSpeed = 2f;

    private bool hasMoved = false;
    private Coroutine currentRoutine;

    void Start()
    {
        moveTextGroup.alpha = 0f;
        moveTextGroup.gameObject.SetActive(true);

        Invoke(nameof(StartFadeIn), delay);
    }

    void Update()
    {
        if (!hasMoved && (Input.GetKey(KeyCode.W) ||
                          Input.GetKey(KeyCode.A) ||
                          Input.GetKey(KeyCode.S) ||
                          Input.GetKey(KeyCode.D)))
        {
            hasMoved = true;

            if (currentRoutine != null)
                StopCoroutine(currentRoutine);

            currentRoutine = StartCoroutine(FadeOut());
        }
    }

    void StartFadeIn()
    {
        if (currentRoutine != null)
            StopCoroutine(currentRoutine);

        currentRoutine = StartCoroutine(FadeIn());
    }

    IEnumerator FadeIn()
    {
        moveTextGroup.gameObject.SetActive(true);
        moveTextGroup.alpha = 0f;

        while (moveTextGroup.alpha < 1f)
        {
            moveTextGroup.alpha += Time.deltaTime * fadeSpeed;
            yield return null;
        }

        moveTextGroup.alpha = 1f;
        currentRoutine = null;
    }

    IEnumerator FadeOut()
    {
        Debug.Log("FadeOut started");

        while (moveTextGroup.alpha > 0.01f)
        {
            moveTextGroup.alpha -= Time.deltaTime * fadeSpeed;
            yield return null;
        }

        moveTextGroup.alpha = 0f;
        moveTextGroup.gameObject.SetActive(false);
        currentRoutine = null;
    }
}