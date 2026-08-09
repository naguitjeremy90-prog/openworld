using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneTeleport : MonoBehaviour
{
    public string sceneName;

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (GameFlags.canTeleport)
            {
                SceneManager.LoadScene(sceneName);
            }
            else
            {
                Debug.Log("You can't enter yet. Talk to the NPC first!");
            }
        }
    }
}