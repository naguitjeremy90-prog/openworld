using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DialogueEditor;

public class womanconversation : MonoBehaviour
{
    [SerializeField] private NPCConversation myConversation;
    [SerializeField] private GameObject talkText;
    [SerializeField] private GameObject closeUpCamera;

    private bool playerNear = false;
    private bool isTalking = false;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerNear = true;

            if (!isTalking && talkText != null)
                talkText.SetActive(true);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerNear = false;
            EndConversation();
        }
    }

    private void Update()
    {
        if (playerNear && !isTalking && Input.GetKeyDown(KeyCode.E))
        {
            StartConversation();
        }
    }

    private void StartConversation()
    {
        isTalking = true;

        if (talkText != null)
            talkText.SetActive(false); // hide text when dialogue starts

        if (closeUpCamera != null)
            closeUpCamera.SetActive(true);

        ConversationManager.Instance.StartConversation(myConversation);
    }

    public void EndConversation()
    {
        isTalking = false;

        if (closeUpCamera != null)
            closeUpCamera.SetActive(false);

        if (talkText != null)
            talkText.SetActive(false);

        ConversationManager.Instance.EndConversation();
    }
}

