using UnityEngine;

public class SectionTrigger : MonoBehaviour
{
    public GameObject RoadSection;

    private void OnTriggerEnter(Collider other)
    {
        if(other.gameObject.CompareTag("Trigger"))
        {
            Instantiate(RoadSection, new Vector3(-5, 0, 0), Quaternion.identity);
        }
    }
}
