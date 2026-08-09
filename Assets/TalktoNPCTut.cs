using UnityEngine;
using System.Collections;

public class TalkTutorial : MonoBehaviour
{
    public CanvasGroup talkGroup;

    void Start()
    {
        StartCoroutine(TutorialFlow());
    }

    IEnumerator TutorialFlow()
    {
        talkGroup.alpha = 0f;
        talkGroup.gameObject.SetActive(true);

        // wait for movement
        while (!PlayerMovement.movementDone)
        {
            yield return null;
        }

        yield return new WaitForSeconds(1f);

        yield return StartCoroutine(FadeIn());

        yield return new WaitForSeconds(5f);

        yield return StartCoroutine(FadeOut());
    }

    IEnumerator FadeIn()
    {
        while (talkGroup.alpha < 1f)
        {
            talkGroup.alpha += Time.deltaTime * 2f;
            yield return null;
        }
    }

    IEnumerator FadeOut()
    {
        while (talkGroup.alpha > 0f)
        {
            talkGroup.alpha -= Time.deltaTime * 2f;
            yield return null;
        }

        talkGroup.gameObject.SetActive(false);
    }
}