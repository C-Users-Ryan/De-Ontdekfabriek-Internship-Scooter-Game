using UnityEngine;

public class SectionTrigger : MonoBehaviour
{
    [Tooltip("Add all your road section prefabs here. One will be picked at random each time.")]
    public GameObject[] roadSections;

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Trigger"))
        {
            // Safety check — do nothing if the array is empty
            if (roadSections == null || roadSections.Length == 0)
            {
                Debug.LogWarning("SectionTrigger: No road sections assigned!");
                return;
            }

            // Pick a random section from the array
            int randomIndex = Random.Range(0, roadSections.Length);
            GameObject chosenSection = roadSections[randomIndex];

            Instantiate(chosenSection, new Vector3(0, 0, -9), Quaternion.identity);
        }
    }

}
