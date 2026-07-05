using UnityEngine;
using KenyaScooter.Core;

namespace KenyaScooter.Environment
{
    /// <summary>
    /// Laundry strung between homestead poles (FX Design Spec "Levend Kenia" §4.6, 3 Jul 2026). Laundry
    /// means THIS MORNING SOMEONE WASHED CLOTHES HERE — domestic life without a single character, and the
    /// bright huisstijl garment colours read from 40 m as the colour accents the research brief asks for.
    ///
    /// No cloth sim: each garment is a hinged quad (pinned at the line) whose swing is base sway + sine
    /// flutter, amplitude and frequency scaled by <see cref="KenyaScooter.FX.WindField"/>, with a sharp
    /// SNAP spike inside a gust front. Hashed phase offsets per garment make the line ripple rather than
    /// march in step. Pure child-transform animation (the RoadsideWaver contract — the spawner owns the
    /// prop root), near-zero cost, and it pairs naturally with a smoke column on the same homestead.
    /// </summary>
    public sealed class ClothesLine : MonoBehaviour
    {
        [Tooltip("Garment pivots, hinged at the line; the cloth hangs down from each. The factory wires these; " +
                 "empty = auto-collect children named 'Garment*'.")]
        [SerializeField] private Transform[] garments;
        [Tooltip("Swing in a dead calm, degrees.")]
        [SerializeField] private float baseAmp = 4f;
        [Tooltip("Extra swing at full wind, degrees.")]
        [SerializeField] private float gustAmp = 22f;

        private Quaternion[] rest;

        private void Awake()
        {
            if (garments == null || garments.Length == 0)
            {
                var found = new System.Collections.Generic.List<Transform>(6);
                foreach (Transform child in transform)
                    if (child.name.StartsWith("Garment"))
                        found.Add(child);
                garments = found.ToArray();
            }
            rest = new Quaternion[garments.Length];
            for (int i = 0; i < garments.Length; i++)
                rest[i] = garments[i] != null ? garments[i].localRotation : Quaternion.identity;
        }

        private void Update()
        {
            GameState state = GameManager.State;
            if (garments == null || (state != GameState.Playing && state != GameState.AtCheckpoint))
                return;

            float w = FX.WindField.Strength01;
            float freq = 3.2f + 8f * w;          // a livelier line in more wind
            float dir = FX.WindField.DirectionX;
            float t = Time.time;
            bool front = FX.WindField.FrontLive;

            for (int i = 0; i < garments.Length; i++)
            {
                if (garments[i] == null)
                    continue;
                float snap = front ? Mathf.Sin(t * 21f + i) * 6f : 0f; // the crack of cloth inside a front
                float ang = (baseAmp + gustAmp * w) * Mathf.Sin(t * freq + i * 1.8f) + snap;
                garments[i].localRotation = rest[i] * Quaternion.Euler(ang * dir, 0f, 0f);
            }
        }
    }
}
