using UnityEngine;
using KenyaScooter.Core;

namespace KenyaScooter.Roads
{
    /// <summary>
    /// Marker on the animal's collider child. PlayerCollisionHandler routes hits on
    /// this through the normal collision pipeline (M15/M16) — a slow brush with a
    /// giraffe is grace-absorbed, a full-speed hit rewinds.
    /// </summary>
    public sealed class WildlifeAnimal : MonoBehaviour { }

    /// <summary>
    /// The wildlife crossing (M22, MDA's flagship discovery moment): an animal walks
    /// perpendicularly across the road when the player approaches. The crossing is
    /// survivable without prior knowledge — the cross speed is slow enough to steer
    /// around even with no warning (design Tension 4: discovery vs legibility). The
    /// warning-sign tile preceding this tile gives attentive players their head start.
    /// Sits on a tsavo_wildlife tile prefab; resets each time the tile is reused.
    /// </summary>
    public sealed class WildlifeCrossing : MonoBehaviour
    {
        [SerializeField] private Transform animal;
        [Tooltip("Metres per second across the road. Keep low enough to dodge reactively.")]
        [SerializeField] private float crossSpeed = 2.5f;
        [Tooltip("The crossing starts when the animal is this far ahead of the player.")]
        [SerializeField] private float activateDistance = 65f;
        [SerializeField] private Vector3 localStart = new Vector3(-8f, 0f, 15f);
        [SerializeField] private Vector3 localEnd = new Vector3(8f, 0f, 15f);

        private bool crossing;
        private bool done;

        private void OnEnable()
        {
            if (animal != null)
                animal.localPosition = localStart;
            crossing = false;
            done = false;
        }

        private void Update()
        {
            GameState state = GameManager.State;
            if (done || animal == null || (state != GameState.Playing && state != GameState.AtCheckpoint))
                return;

            if (!crossing)
            {
                Transform player = GameManager.Player;
                if (player == null)
                    return;

                float ahead = RoadDirection.Longitudinal(animal.position) - RoadDirection.Longitudinal(player.position);
                if (ahead > 0f && ahead < activateDistance)
                    crossing = true;
                return;
            }

            animal.localPosition = Vector3.MoveTowards(animal.localPosition, localEnd, crossSpeed * Time.deltaTime);
            if (animal.localPosition == localEnd)
                done = true;
        }
    }
}
