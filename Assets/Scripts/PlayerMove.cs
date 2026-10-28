using UnityEngine;

public class PlayerMove : MonoBehaviour
{
    [SerializeField] private float speed = 5f;

    void Update()
    {
        float direction = 0f;

        if (Input.GetKey(KeyCode.A)) direction -= 1f;
        if (Input.GetKey(KeyCode.D)) direction += 1f;

        transform.Translate(Vector3.right * direction * speed * Time.deltaTime, Space.World);
    }
}
