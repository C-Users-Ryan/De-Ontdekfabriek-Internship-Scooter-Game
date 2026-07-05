using UnityEngine;
using KenyaScooter.Core;

namespace KenyaScooter.Environment
{
    /// <summary>
    /// Makes a roadside person REACT to the player as they pass: the arm RAISES and waves, and the body turns to
    /// face you, for the big "the world is alive" payoff at tiny cost (Oplevering insight 2: "reactive people who
    /// wave or turn as you pass"). Self-contained: drop it on a person prop prefab and (optionally) assign an arm
    /// to wave and a body to turn.
    ///
    /// The player sits at the world origin (the world scrolls past), so "how close is the player" is just this
    /// prop's planar distance to the origin, the same constant-frame axes everything else projects onto, no
    /// per-frame world-axis code. The wave is procedural (the arm lifts to a raised pose, then a sine swing rocks
    /// the hand) so it needs no animation clip; when the player passes, the arm eases back down. If the prefab
    /// also has an Animator, a trigger is fired as well, so a fully-rigged character and a bare placeholder both
    /// work. Only CHILD transforms are animated, so this never fights the spawner that owns the prop root pose.
    /// </summary>
    public sealed class RoadsideWaver : MonoBehaviour
    {
        [Header("Wave (procedural, no clip needed)")]
        [Tooltip("The arm/hand transform that raises and waves. Empty = no procedural wave (face-turn / Animator still work).")]
        [SerializeField] private Transform waveArm;
        [Tooltip("Local rotation (Euler) the arm eases TO while reacting, i.e. the raised 'waving' pose, relative to " +
                 "its rest pose. The default raises a downward-hanging arm up and out to the side.")]
        [SerializeField] private Vector3 raiseEuler = new Vector3(0f, 0f, 150f);
        [Tooltip("How fast the arm raises and lowers (higher = snappier).")]
        [SerializeField] private float raiseSpeed = 6f;
        [Tooltip("Local axis the raised hand rocks around, and how far (degrees) to either side.")]
        [SerializeField] private Vector3 waveAxis = Vector3.forward;
        [SerializeField] private float waveAmplitude = 16f;
        [Tooltip("Wave speed in rocks per second.")]
        [SerializeField] private float waveFrequency = 3f;

        [Header("Face the player")]
        [Tooltip("Optional body/head transform that yaws to face the passing player. Empty = no facing.")]
        [SerializeField] private Transform faceBody;
        [Tooltip("How quickly the body turns to face the player (higher = snappier).")]
        [SerializeField] private float faceTurnSpeed = 4f;

        [Header("Trigger")]
        [Tooltip("Planar distance (m) to the player at which the person starts reacting. The player is at the origin.")]
        [SerializeField] private float reactRange = 24f;
        [Tooltip("Optional. If the prefab has an Animator, this trigger is set once when the player comes into range " +
                 "(e.g. a 'Wave' state). Leave blank to rely only on the procedural wave.")]
        [SerializeField] private string animatorTrigger = "Wave";

        private Animator animator;
        private Quaternion armRest;
        private Quaternion armRaised;
        private float lift;        // 0 = arm down (rest), 1 = arm fully raised; eases between
        private bool reacting;

        private void Awake()
        {
            animator = GetComponentInChildren<Animator>();
            if (waveArm != null)
            {
                armRest = waveArm.localRotation;
                armRaised = armRest * Quaternion.Euler(raiseEuler);
            }
        }

        private void OnEnable()
        {
            // Fresh from the pool: reset to the resting pose so a reused person never starts mid-wave.
            reacting = false;
            lift = 0f;
            if (waveArm != null)
                waveArm.localRotation = armRest;
        }

        private void Update()
        {
            GameState state = GameManager.State;
            if (state != GameState.Playing && state != GameState.AtCheckpoint)
                return;

            Vector3 player = GameManager.Player != null ? GameManager.Player.position : Vector3.zero;
            Vector3 here = transform.position;
            float dx = here.x - player.x;
            float dz = here.z - player.z;
            bool inRange = (dx * dx + dz * dz) <= reactRange * reactRange;

            if (inRange && !reacting)
            {
                reacting = true;
                if (animator != null && !string.IsNullOrEmpty(animatorTrigger))
                    animator.SetTrigger(animatorTrigger);
            }
            else if (!inRange)
            {
                reacting = false;
            }

            // Ease the arm up while reacting, back down when the player has gone; overlay the rocking wave only at lift.
            if (waveArm != null)
            {
                lift = Mathf.MoveTowards(lift, reacting ? 1f : 0f, Time.deltaTime * raiseSpeed);
                Quaternion baseRot = Quaternion.Slerp(armRest, armRaised, lift);
                float swing = (lift > 0.01f) ? Mathf.Sin(Time.time * waveFrequency * Mathf.PI * 2f) * waveAmplitude * lift : 0f;
                waveArm.localRotation = baseRot * Quaternion.AngleAxis(swing, waveAxis);
            }

            if (reacting && faceBody != null)
            {
                Vector3 toPlayer = player - faceBody.position;
                toPlayer.y = 0f;
                if (toPlayer.sqrMagnitude > 0.0001f)
                {
                    Quaternion look = Quaternion.LookRotation(toPlayer.normalized, Vector3.up);
                    faceBody.rotation = Quaternion.Slerp(faceBody.rotation, look, Time.deltaTime * faceTurnSpeed);
                }
            }
        }
    }
}
