using UnityEngine;

public class SectionTrigger : MonoBehaviour
{
    public GameObject RoadSection;

    private void OnTriggerEnter(Collider other)
    {
        if(other.gameObject.CompareTag("Trigger"))
        {
            Instantiate(RoadSection, new Vector3(0, 0, -9), Quaternion.identity);
        }
    }

}
