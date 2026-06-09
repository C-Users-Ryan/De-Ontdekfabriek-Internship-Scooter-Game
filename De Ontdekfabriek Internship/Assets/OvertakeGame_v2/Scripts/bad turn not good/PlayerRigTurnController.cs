using System.Collections;
using UnityEngine;

public class PlayerRigTurnController : MonoBehaviour
{
    private Coroutine rotateRoutine;

    public void TurnDegrees(float degrees, float duration)
    {
        if (rotateRoutine != null)
            StopCoroutine(rotateRoutine);

        rotateRoutine = StartCoroutine(TurnRoutine(degrees, duration));
    }

    private IEnumerator TurnRoutine(float degrees, float duration)
    {
        Quaternion startRotation = transform.rotation;
        Quaternion endRotation = startRotation * Quaternion.Euler(0f, degrees, 0f);

        float time = 0f;

        while (time < duration)
        {
            time += Time.deltaTime;

            float t = Mathf.Clamp01(time / duration);

            transform.rotation = Quaternion.Slerp(startRotation, endRotation, t);

            yield return null;
        }

        transform.rotation = endRotation;
    }
}