using UnityEngine;

public class LoopingBackgroundChunk : MonoBehaviour
{
    [SerializeField] private float speed = 5f;
    [SerializeField] private float resetX = -20f;
    [SerializeField] private float spawnX = 40f;

    private void Update()
    {
        transform.Translate(Vector3.left * speed * Time.deltaTime, Space.World);

        if (transform.position.x <= resetX)
        {
            Vector3 pos = transform.position;
            pos.x = spawnX;
            transform.position = pos;
        }
    }
}
