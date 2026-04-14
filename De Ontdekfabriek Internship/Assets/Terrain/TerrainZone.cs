using UnityEngine;

/// <summary>
/// TerrainZone — Attach this to any road section prefab.
/// Add a Box Collider set to "Is Trigger" on the same GameObject.
/// Assign the matching TerrainProfile in the Inspector.
///
/// When the player enters this zone, the TerrainBehaviourController
/// on the player will automatically blend into this terrain's behaviour.
/// </summary>
public class TerrainZone : MonoBehaviour
{
    [Tooltip("The terrain profile this road section uses.")]
    public TerrainProfile terrainProfile;

    private void OnTriggerEnter(Collider other)
    {
        TerrainBehaviourController terrain = other.GetComponent<TerrainBehaviourController>();
        if (terrain != null)
            terrain.EnterTerrain(terrainProfile);
    }

    private void OnTriggerExit(Collider other)
    {
        TerrainBehaviourController terrain = other.GetComponent<TerrainBehaviourController>();
        if (terrain != null)
            terrain.ExitTerrain(terrainProfile);
    }
}
