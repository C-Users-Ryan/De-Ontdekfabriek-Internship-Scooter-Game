using UnityEngine;

namespace KenyaScooter.SafetyNet
{
    /// <summary>One snapshot of one participant, stored in the rewind ring buffer (M17).</summary>
    public struct RewindSample
    {
        public Vector3 Position;
        public Quaternion Rotation;
        /// <summary>Participant-defined extra value (player: lateral velocity, vehicle: own speed).</summary>
        public float Aux;
        /// <summary>Whether the participant's GameObject was active at capture time.</summary>
        public bool Active;
    }

    /// <summary>
    /// Anything the rewind records and restores: the player, and every pooled traffic vehicle, hazard
    /// and road tile. Each instance registers once when it's created (pools never destroy), so its slot
    /// in the buffer stays the same for the whole run.
    /// </summary>
    public interface IRewindable
    {
        void CaptureSample(ref RewindSample sample);
        void ApplySample(in RewindSample sample);
    }
}
