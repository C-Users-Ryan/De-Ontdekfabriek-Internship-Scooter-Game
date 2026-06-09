using UnityEngine;

public class ConstantForwardMovement : MonoBehaviour
{
    [SerializeField] private Transform directionSource; // PlayerRig
    [SerializeField] private float speed = 5f;

    private void Update()
    {
        Vector3 dir = directionSource.forward;
        dir.y = 0f;
        dir.Normalize();

        transform.position += dir * speed * Time.deltaTime;
    }
}